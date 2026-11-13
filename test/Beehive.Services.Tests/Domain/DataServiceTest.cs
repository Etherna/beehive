// Copyright 2021-present Etherna SA
// This file is part of Beehive.
//
// Beehive is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
//
// Beehive is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License along with Beehive.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Beehive.Domain.Exceptions;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Helpers;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using PostageStamp = Etherna.Beehive.Domain.Models.PostageStamp;

namespace Etherna.Beehive.Services.Domain
{
    public class DataServiceTest
    {
        // Consts.
        private const string NodeId = "nodeA";
        private static readonly PostageBatchId BatchId = "aa00000000000000000000000000000000000000000000000000000000000001";
        private static readonly PostageBatchId UnknownBatchId = "aa00000000000000000000000000000000000000000000000000000000000002";

        // Fields.
        private readonly Mock<IRepository<PushingChunkRef, string>> chunkPushQueueMock = new();
        private readonly InMemoryBeehiveDb db = new();
        private readonly Mock<IPostageBatchService> postageBatchServiceMock = new();
        private readonly DataService service;

        // Constructor.
        public DataServiceTest()
        {
            // A batch cache knowing one batch, free to lock and usable on its node: an upload on it reaches the chunk store.
            var cachedBatches = new List<PostageBatchCache>
            {
                new(BatchId, new uint[PostageBuckets.BucketsSize], 17, false, NodeId)
            };
            var postageBatchesCacheMock = new Mock<IRepository<PostageBatchCache, string>>();
            postageBatchesCacheMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<PostageBatchCache, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<PostageBatchCache, bool>> predicate, CancellationToken _) =>
                    cachedBatches.AsQueryable().FirstOrDefault(predicate));
            var postageStampsMock = new Mock<IRepository<PostageStamp, string>>();
            postageStampsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<PostageStamp>, Task<List<PostageStamp>>>>(),
                    null))
                .Returns((Func<IQueryable<PostageStamp>, Task<List<PostageStamp>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(new List<PostageStamp>())));
            db.DbContextMock.Setup(c => c.ChunkPushQueue).Returns(chunkPushQueueMock.Object);
            db.DbContextMock.Setup(c => c.PostageBatchesCache).Returns(postageBatchesCacheMock.Object);
            db.DbContextMock.Setup(c => c.PostageStamps).Returns(postageStampsMock.Object);

            // The batch lock is granted: the lookup runs inside it.
            postageBatchServiceMock.Setup(s => s.AcquireLockAsync(It.IsAny<PostageBatchId>(), It.IsAny<bool>()))
                .ReturnsAsync((PostageBatchId batchId, bool exclusiveAccess) => new ResourceLockHandler<PostageBatchLock>( //disposed by the upload
                    new Mock<IRepository<PostageBatchLock, string>>().Object,
                    new Mock<IResourceLockService>().Object,
                    batchId.ToString(),
                    exclusiveAccess));

            service = new DataService(
                new Mock<IBeeNodeLiveManager>().Object,
                db.DbContext,
                NullLogger<DataService>.Instance,
                postageBatchServiceMock.Object,
                new Mock<ISerializerModifierAccessor>().Object);
        }

        // Tests.

        [Fact]
        public async Task UploadFailsAtTheRefusedWriteWithoutQueueingNorPinning()
        {
            // Setup.
            //the db refuses the chunks
            var dbFailure = new MongoException("db down");
            db.ChunksMock.Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(dbFailure);

            // Action.
            var exception = await Assert.ThrowsAsync<MongoException>(() => service.UploadAsync(
                BatchId,
                null,
                false,
                true,
                async (chunkStore, postageStamper) =>
                {
                    var bmt = new SwarmChunkBmt();
                    var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, Enumerable.Range(0, 42).Select(i => (byte)i).ToArray()).SpanData;
                    var chunk = new SwarmCac(bmt.Hash(spanData), spanData);

                    postageStamper.Stamp(chunk.Hash);
                    await chunkStore.AddAsync(chunk);

                    return new SwarmReference(chunk.Hash, null);
                }));

            // Assert.
            Assert.Same(dbFailure, exception);
            //one refused write, the flush of the upload: the store's dispose didn't try it again
            db.ChunksMock.Verify(
                r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()),
                Times.Once);
            //nothing recorded as succeeded: no stamps, no push, the pin still provisional
            postageBatchServiceMock.Verify(
                s => s.StoreStampedChunksAsync(It.IsAny<PostageBatchCache>(), It.IsAny<HashSet<SwarmHash>>(), It.IsAny<IPostageStamper>()),
                Times.Never);
            chunkPushQueueMock.Verify(
                r => r.CreateAsync(It.IsAny<IEnumerable<PushingChunkRef>>(), It.IsAny<CancellationToken>()),
                Times.Never);
            var pin = Assert.Single(db.ChunkPins);
            Assert.False(pin.IsProcessed);
            db.DbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UploadOnABatchUnknownToBeehiveIsRefusedAsBatchNotFound()
        {
            // Setup.
            var chunkingInvoked = false;

            // Action.
            var exception = await Assert.ThrowsAsync<PostageBatchNotFoundException>(() => service.UploadAsync(
                UnknownBatchId,
                null,
                false,
                false,
                (_, _) =>
                {
                    chunkingInvoked = true;
                    return Task.FromResult(new SwarmReference(SwarmHash.Zero, null));
                }));

            // Assert.
            Assert.Contains(UnknownBatchId.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.False(chunkingInvoked); //refused before anything is stored
        }
    }
}

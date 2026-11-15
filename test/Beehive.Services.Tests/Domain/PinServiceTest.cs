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

using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Helpers;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Domain
{
    public class PinServiceTest
    {
        // Consts.
        private const string NodeId = "nodeA";
        private const string NodeOverlay = "aa00000000000000000000000000000000000000000000000000000000000001";
        private const string PrivateKeyHex = "0x4f3edf983ac636a65a842ce7c78d9aa706d3b113bce9c46f30d7d21715b23b1d";
        //three levels: 150 data chunks under 2 intermediate chunks under the root
        private const int ContentLength = 600 * 1024;

        // Fields.
        private readonly InMemoryBeehiveDb db = new();
        private readonly FakeBeeNode node = new(NodeId, NodeOverlay);
        private readonly PinService service;

        // Constructor.
        public PinServiceTest()
        {
            // The node is the only healthy one: the nearest for every hash, and the whole partition.
            var beeNodeLiveManagerMock = new Mock<IBeeNodeLiveManager>();
            beeNodeLiveManagerMock.Setup(m => m.HealthyNodes).Returns(() => [node.Instance]);
            beeNodeLiveManagerMock.Setup(m => m.SelectNearestHealthyNode(It.IsAny<SwarmHash>())).Returns(node.Instance);

            // Every lock is granted.
            var resourceLockServiceMock = new Mock<IResourceLockService>();
            resourceLockServiceMock.Setup(s => s.TryAcquireLockAsync(
                    It.IsAny<Func<ChunkPinLock>>(),
                    It.IsAny<IRepository<ChunkPinLock, string>>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()))
                .Returns((Func<ChunkPinLock> _, IRepository<ChunkPinLock, string> repository, string resourceId, bool exclusiveAccess) =>
                    Task.FromResult<ResourceLockHandler<ChunkPinLock>?>(
                        new ResourceLockHandler<ChunkPinLock>(repository, resourceLockServiceMock.Object, resourceId, exclusiveAccess)));

            service = new PinService(
                beeNodeLiveManagerMock.Object,
                db.DbContext,
                NullLogger<PinService>.Instance,
                resourceLockServiceMock.Object,
                new Mock<ISerializerModifierAccessor>().Object);
        }

        // Tests.

        [Fact]
        public async Task CountsAsMissingAChunkWhoseDocumentIsGoneWhenThePinIsAttached()
        {
            // Setup.
            //a data chunk document deleted between its fetch and the pinning: the add-to-set finds nothing
            var reference = await UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);
            var goneHash = node.Chunks.AllChunks.Keys.First(h => h != reference.Hash);
            db.ChunksMock.Setup(r => r.TryFindOneAndAddToSetAsync(
                    It.IsAny<FilterDefinition<Chunk>>(),
                    It.IsAny<Expression<Func<Chunk, IEnumerable<ChunkPin>>>>(),
                    It.IsAny<ChunkPin>(),
                    It.IsAny<FindOneAndUpdateOptions<Chunk>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    FilterDefinition<Chunk> filter,
                    Expression<Func<Chunk, IEnumerable<ChunkPin>>> _,
                    ChunkPin addedPin,
                    FindOneAndUpdateOptions<Chunk> _,
                    CancellationToken _) =>
                {
                    var chunk = db.Chunks.FirstOrDefault(((ExpressionFilterDefinition<Chunk>)filter).Expression.Compile());
                    if (chunk is null)
                        return Task.FromResult<Chunk?>(null);
                    if (chunk.Hash == goneHash)
                    {
                        db.Chunks.Remove(chunk);
                        return Task.FromResult<Chunk?>(null);
                    }
                    if (chunk.Pins.All(p => p.Id != addedPin.Id))
                        chunk.AddPin(addedPin);
                    return Task.FromResult<Chunk?>(chunk);
                });

            // Action.
            var result = await service.PinChunksAsync(pin.Id, node.Instance);

            // Assert.
            Assert.False(result.Pin.IsSucceeded);
            Assert.Equal([goneHash], result.Pin.MissingChunks);
            Assert.Equal(node.Chunks.AllChunks.Count - 1, result.Pin.TotPinnedChunks);
            Assert.DoesNotContain(db.Chunks, c => c.Hash == goneHash);
            Assert.All(db.Chunks, c => Assert.Equal([pin], c.Pins));
        }

        [Fact]
        public async Task FailsThePinWithTheChunksWhoseDocumentDidNotTakeThePin()
        {
            // Setup.
            //beehive holds the content already, and the add-to-set answers the documents without writing the pin
            var reference = await UploadContentAsync(node.Chunks);
            foreach (var chunk in node.Chunks.AllChunks.Values)
                db.AddChunk(new Chunk(chunk.Hash, chunk.GetFullPayload(), false));
            var pin = NewPin(reference);
            db.ChunksMock.Setup(r => r.TryFindOneAndAddToSetAsync(
                    It.IsAny<FilterDefinition<Chunk>>(),
                    It.IsAny<Expression<Func<Chunk, IEnumerable<ChunkPin>>>>(),
                    It.IsAny<ChunkPin>(),
                    It.IsAny<FindOneAndUpdateOptions<Chunk>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    FilterDefinition<Chunk> filter,
                    Expression<Func<Chunk, IEnumerable<ChunkPin>>> _,
                    ChunkPin _,
                    FindOneAndUpdateOptions<Chunk> _,
                    CancellationToken _) =>
                    Task.FromResult(db.Chunks.FirstOrDefault(((ExpressionFilterDefinition<Chunk>)filter).Expression.Compile())));

            // Action.
            var result = await service.PinChunksAsync(pin.Id, node.Instance);

            // Assert.
            Assert.False(result.Pin.IsSucceeded);
            Assert.Equal(0, result.Pin.TotPinnedChunks);
            Assert.Equal(node.Chunks.AllChunks.Count, result.Pin.MissingChunks.Count());
            Assert.Empty(node.RequestedHashes); //nothing was read from the node
        }

        [Fact]
        public async Task PinsAContentReadFromTheHealthyNodesWithoutASourceNode()
        {
            // Setup.
            var reference = await UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);

            // Action.
            var result = await service.PinChunksAsync(pin.Id, null);

            // Assert.
            AssertContentPinned(result, pin);
        }

        [Fact]
        public async Task PinsAContentReadFromTheSourceNodeSavingEveryChunkWithThePin()
        {
            // Setup.
            var reference = await UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);

            // Action.
            var result = await service.PinChunksAsync(pin.Id, node.Instance);

            // Assert.
            AssertContentPinned(result, pin);
        }

        [Fact]
        public async Task PinsASingleOwnerChunkReferenceAsItsSingleChunk()
        {
            // Setup.
            //a soc pinned on the node, nothing in the db: as bee, the wrapped chunk is not entered
            var soc = NewSignedSoc();
            await node.Chunks.AddAsync(soc);
            var pin = NewPin(new SwarmReference(soc.Hash, null));

            // Action.
            var result = await service.PinChunksAsync(pin.Id, node.Instance);

            // Assert.
            Assert.True(result.Pin.IsSucceeded);
            Assert.Equal(1, result.Pin.TotPinnedChunks);
            Assert.Empty(result.InvalidChunks);
            var chunk = Assert.Single(db.Chunks);
            Assert.Equal(soc.Hash, chunk.Hash);
            Assert.True(chunk.IsSoc);
            Assert.Contains(pin, chunk.Pins);
        }

        // Helpers.
        private void AssertContentPinned(Utilities.Models.PinChunksResult result, ChunkPin pin)
        {
            var contentHashes = node.Chunks.AllChunks.Keys.ToHashSet();
            Assert.True(contentHashes.Count > 130); //a three level content, not a tiny one

            Assert.Same(pin, result.Pin);
            Assert.True(result.Pin.IsSucceeded);
            Assert.Empty(result.InvalidChunks);
            Assert.Equal(contentHashes.Count, result.Pin.TotPinnedChunks);

            //every chunk of the content is on the db, once, with the pin once
            Assert.Equal(contentHashes.Count, db.Chunks.Count);
            Assert.Equal(contentHashes, db.Chunks.Select(c => c.Hash).ToHashSet());
            Assert.All(db.Chunks, c => Assert.Equal([pin], c.Pins));
        }

        private ChunkPin NewPin(SwarmReference reference)
        {
            var pin = new ChunkPin(reference);
            db.AddPin(pin);
            return pin;
        }

        private static SwarmSoc NewSignedSoc()
        {
            var bmt = new SwarmChunkBmt();
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, Enumerable.Range(0, 42).Select(i => (byte)i).ToArray()).SpanData;
            var innerChunk = new SwarmCac(bmt.Hash(spanData), spanData);
            var signer = new PrivateKeySigner(new EthPrivateKey(PrivateKeyHex));
            SwarmSocIdentifier identifier = Enumerable.Range(0, SwarmSocIdentifier.IdentifierSize).Select(i => (byte)i).ToArray();
            var soc = new SwarmSoc(identifier, signer.PublicAddress, innerChunk);
            soc.Sign(signer, new Hasher());
            return soc;
        }

        /// <summary>
        /// Build a content with the sdk hasher pipeline into the given store, as an upload would: deterministic
        /// pseudo-random data, so that no two data chunks are the same
        /// </summary>
        private static async Task<SwarmReference> UploadContentAsync(IChunkStore chunkStore)
        {
            var data = new byte[ContentLength];
            var state = 2463534242u; //xorshift32
            for (var i = 0; i < data.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                data[i] = (byte)state;
            }

            using var hasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                new FakePostageStamper(),
                RedundancyLevel.None,
                false,
                0,
                null);
            return await hasherPipeline.HashDataAsync(data);
        }
    }
}

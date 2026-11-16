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

using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Helpers;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.GridFS;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.ExecContext.AsyncLocal;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public class BytesApiHandlerTest
    {
        // Tests.

        [Fact]
        public async Task DownloadServesTheChunksReadFromTheNodesWhileTheResponseIsWritten()
        {
            // Setup.
            //beehive holds the root chunk of a 600 KiB content and none of its other chunks; a healthy node holds them all
            var content = new byte[600 * 1024];
            RandomNumberGenerator.Fill(content);
            var nodeChunks = new MemoryChunkStore();
            using var hasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                nodeChunks,
                new FakePostageStamper(),
                RedundancyLevel.None,
                false,
                0,
                null);
            var reference = await hasherPipeline.HashDataAsync(content);
            var root = nodeChunks.AllChunks[reference.Hash];
            var dbChunks = new List<Chunk> { new(root.Hash, root.GetFullPayload(), false) };

            var chunksMock = new Mock<IRepository<Chunk, string>>();
            chunksMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<Chunk, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Chunk, bool>> predicate, CancellationToken _) =>
                    dbChunks.FirstOrDefault(predicate.Compile()));
            chunksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<Chunk>, Task<List<Chunk>>>>(),
                    null))
                .Returns((Func<IQueryable<Chunk>, Task<List<Chunk>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(dbChunks.ToArray())));
            chunksMock.Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()))
                .Callback((IEnumerable<Chunk> chunks, CancellationToken _) => dbChunks.AddRange(chunks))
                .Returns(Task.CompletedTask);
            var chunksBucketMock = new Mock<IGridFSBucket>();
            chunksBucketMock.Setup(b => b.DownloadAsBytesByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<GridFSDownloadByNameOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new GridFSFileNotFoundException("chunk", -1));
            var engineMock = new Mock<IDbContextEngine>();
            engineMock.Setup(e => e.ExecutionContext).Returns(AsyncLocalContext.Instance);
            var dbContextMock = new Mock<IBeehiveDbContext>();
            dbContextMock.Setup(c => c.Chunks).Returns(chunksMock.Object);
            dbContextMock.Setup(c => c.ChunksBucket).Returns(chunksBucketMock.Object);
            dbContextMock.Setup(c => c.Engine).Returns(engineMock.Object);

            var node = NewAliveNode(nodeChunks);
            var beeNodeLiveManagerMock = new Mock<IBeeNodeLiveManager>();
            beeNodeLiveManagerMock.Setup(m => m.HealthyNodes).Returns([node]);
            beeNodeLiveManagerMock.Setup(m => m.SelectNearestHealthyNode(It.IsAny<SwarmHash>())).Returns(node);

            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            httpContext.Response.Body = new MemoryStream();
            var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            httpContextAccessorMock.Setup(a => a.HttpContext).Returns(httpContext);

            var handler = new BytesApiHandler(
                beeNodeLiveManagerMock.Object,
                new Mock<IDataService>().Object,
                dbContextMock.Object,
                httpContextAccessorMock.Object,
                NullLogger<BytesApiHandler>.Instance,
                new Mock<ISerializerModifierAccessor>().Object);

            // Action.
            //the handler returns the stream result, then the host writes the body: the chunks are read from the node now
            var result = await handler.DownloadBytesAsync(reference, RedundancyLevel.None, RedundancyStrategy.None, false);
            await result.ExecuteAsync(httpContext);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
            Assert.Equal(content, ((MemoryStream)httpContext.Response.Body).ToArray());
            Assert.Equal(nodeChunks.AllChunks.Count, dbChunks.Count); //every chunk read from the node was saved
        }

        // Helpers.
        /// <summary>
        /// A bee node alive for the live manager, serving the chunks of the given store and bee's 404 for the others
        /// </summary>
        private static BeeNodeLiveInstance NewAliveNode(MemoryChunkStore chunks)
        {
            var clientMock = new Mock<ISwarmClient>();
            clientMock.Setup(c => c.GetChunkAsync(
                    It.IsAny<SwarmHash>(),
                    It.IsAny<SwarmChunkBmt>(),
                    It.IsAny<bool?>(),
                    It.IsAny<long?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (
                    SwarmHash hash,
                    SwarmChunkBmt _,
                    bool? _,
                    long? _,
                    string? _,
                    string? _,
                    CancellationToken cancellationToken) =>
                    await chunks.TryGetAsync(hash, cancellationToken) ??
                    throw new SwarmSdkApiException(
                        "The HTTP status code of the response was not expected (404).",
                        404,
                        $$$"""{"code":404,"message":"chunk {{{hash}}}"}""",
                        new Dictionary<string, IEnumerable<string>>(),
                        null));

            var node = new BeeNodeLiveInstance("node", clientMock.Object, true, NullLogger.Instance);
            node.Status.SucceededHeartbeatAttempt(DateTime.UtcNow);
            node.Status.InitializeAddresses(new BeeNodeAddresses(
                "0x0000000000000000000000000000000000000001",
                "0000000000000000000000000000000000000000000000000000000000000001",
                "020000000000000000000000000000000000000000000000000000000000000001",
                "020000000000000000000000000000000000000000000000000000000000000001"));
            return node;
        }
    }
}

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

using Etherna.Beehive.Areas.Api;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.GridFS;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.ExecContext.AsyncLocal;
using Etherna.Scrinium.Core.Repositories;
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Extensions;
using Etherna.SwarmSdk.Hashing;
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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Helpers
{
    /// <summary>
    /// What an api handler test needs around the handler: a db holding chunk documents, and taking the ones the
    /// store saves, the deprecated chunks and the legacy bucket empty, the live manager with one healthy node or none, and an http context
    /// whose response body can be read back after the result executed, through the api pipeline or not
    /// </summary>
    public sealed class HandlerTestEnvironment
    {
        // Constructor.
        public HandlerTestEnvironment(BeeNodeLiveInstance? node = null)
        {
            var swarmChunksMock = new Mock<IRepository<Chunk, SwarmHash>>();
            swarmChunksMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<SwarmHash>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwarmHash hash, CancellationToken _) =>
                    DbChunks.FirstOrDefault(c => c.Id == hash));
            swarmChunksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<Chunk>, Task<List<Chunk>>>>(),
                    null))
                .Returns((Func<IQueryable<Chunk>, Task<List<Chunk>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(DbChunks.ToArray())));
            swarmChunksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<Chunk>, Task<List<SwarmHash>>>>(),
                    null))
                .Returns((Func<IQueryable<Chunk>, Task<List<SwarmHash>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(DbChunks.ToArray())));
            swarmChunksMock.Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()))
                .Callback((IEnumerable<Chunk> chunks, CancellationToken _) =>
                {
                    foreach (var chunk in chunks)
                        DbChunks.Add(chunk);
                })
                .Returns(Task.CompletedTask);
            var chunksMock = new Mock<IRepository<LegacyChunk, string>>();
            chunksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<LegacyChunk>, Task<List<LegacyChunk>>>>(),
                    null))
                .Returns((Func<IQueryable<LegacyChunk>, Task<List<LegacyChunk>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(Array.Empty<LegacyChunk>())));
            var chunksBucketMock = new Mock<IGridFSBucket>();
            chunksBucketMock.Setup(b => b.DownloadAsBytesByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<GridFSDownloadByNameOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new GridFSFileNotFoundException("chunk", -1));
            var engineMock = new Mock<IDbContextEngine>();
            engineMock.Setup(e => e.ExecutionContext).Returns(AsyncLocalContext.Instance);
            var dbContextMock = new Mock<IBeehiveDbContext>();
            dbContextMock.Setup(c => c.SwarmChunks).Returns(swarmChunksMock.Object);
            dbContextMock.Setup(c => c.ChunksBucket).Returns(chunksBucketMock.Object);
            dbContextMock.Setup(c => c.Engine).Returns(engineMock.Object);
            dbContextMock.Setup(c => c.Chunks).Returns(chunksMock.Object);
            DbContext = dbContextMock.Object;

            BeeNodeLiveInstance[] healthyNodes = node is null ? [] : [node];
            var beeNodeLiveManagerMock = new Mock<IBeeNodeLiveManager>();
            beeNodeLiveManagerMock.Setup(m => m.HealthyNodes).Returns(healthyNodes);
            if (node is not null)
                beeNodeLiveManagerMock.Setup(m => m.SelectNearestHealthyNode(It.IsAny<SwarmHash>())).Returns(node);
            BeeNodeLiveManager = beeNodeLiveManagerMock.Object;

            HttpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            HttpContext.Response.Body = new MemoryStream();
            var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            httpContextAccessorMock.Setup(a => a.HttpContext).Returns(HttpContext);
            HttpContextAccessor = httpContextAccessorMock.Object;
        }

        // Properties.
        public IBeeNodeLiveManager BeeNodeLiveManager { get; }
        public Collection<Chunk> DbChunks { get; } = [];
        public IBeehiveDbContext DbContext { get; }
        public DefaultHttpContext HttpContext { get; }
        public IHttpContextAccessor HttpContextAccessor { get; }

        // Methods.
        /// <summary>
        /// Run a handler call as the swarm api pipeline does: the result executes on the response, and a failure of
        /// the handler or of the result is answered by <see cref="ApiExceptionMiddleware"/>
        /// </summary>
        public Task ExecuteSwarmApiAsync(Func<Task<IResult>> handlerCall)
        {
            ArgumentNullException.ThrowIfNull(handlerCall);

            HttpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new SwarmApiMarker()), "test"));
            var middleware = new ApiExceptionMiddleware(async context =>
            {
                var result = await handlerCall();
                await result.ExecuteAsync(context);
            });
            return middleware.InvokeAsync(HttpContext);
        }

        /// <summary>
        /// The response body written so far by the executed result
        /// </summary>
        public byte[] GetResponseBody() => ((MemoryStream)HttpContext.Response.Body).ToArray();

        // Static methods.
        /// <summary>
        /// The hashes of the children of a content's root chunk, in content order
        /// </summary>
        public static SwarmHash[] ChildHashesOf(SwarmReference reference, MemoryChunkStore chunks)
        {
            ArgumentNullException.ThrowIfNull(chunks);

            var root = (SwarmCac)chunks.AllChunks[reference.Hash];
            return ((SwarmDecodedIntermediateCac)root.Decode(reference, new Hasher())).ChildReferences
                .Select(r => r.Reference.Hash)
                .ToArray();
        }

        /// <summary>
        /// The db document of a chunk, as the store writes it
        /// </summary>
        public static Chunk DocumentOf(SwarmChunk chunk)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            return new Chunk(chunk.Hash, chunk.GetFullPayload(), chunk is SwarmSoc);
        }

        /// <summary>
        /// A bee node alive for the live manager, serving the chunks of the given store and bee's 404 for the others
        /// </summary>
        public static BeeNodeLiveInstance NewAliveNode(MemoryChunkStore chunks)
        {
            ArgumentNullException.ThrowIfNull(chunks);

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

        /// <summary>
        /// A random content hashed with the sdk pipeline, its chunks in the given store
        /// </summary>
        public static async Task<(SwarmReference Reference, byte[] Content)> NewContentAsync(int size, MemoryChunkStore chunks)
        {
            var content = new byte[size];
            RandomNumberGenerator.Fill(content);
            using var hasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunks,
                new FakePostageStamper(),
                RedundancyLevel.None,
                false,
                0,
                null);
            return (await hasherPipeline.HashDataAsync(content), content);
        }
    }
}

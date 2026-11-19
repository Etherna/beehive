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
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Helpers;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using PostageStamp = Etherna.SwarmSdk.Models.PostageStamp;

namespace Etherna.Beehive.Services.Tasks.Background
{
    public sealed class PushChunksBackgroundServiceTest : IDisposable
    {
        // Consts.
        private static readonly PostageBatchId BatchA = "aa00000000000000000000000000000000000000000000000000000000000001";
        private static readonly PostageBatchId BatchB = "bb00000000000000000000000000000000000000000000000000000000000002";
        private static readonly DateTime FirstQueuedTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private const string PrivateKeyHex = "0x4f3edf983ac636a65a842ce7c78d9aa706d3b113bce9c46f30d7d21715b23b1d";

        // Fields.
        private readonly InMemoryBeehiveDb db = new();
        private readonly HashSet<PostageBatchId> lockedBatches = []; //held by another instance
        private readonly CapturingLogger<PushChunksBackgroundService> logger = new();
        private readonly FakeBeeNode nodeA = new("nodeA", "aa00000000000000000000000000000000000000000000000000000000000000");
        private readonly FakeBeeNode nodeB = new("nodeB", "bb00000000000000000000000000000000000000000000000000000000000000");
        private Action? onPush; //run by a node as a push reaches it
        private readonly Dictionary<PostageBatchId, BeeNodeLiveInstance> ownerNodes = [];
        private readonly List<SwarmHash> pushedHashes = [];
        private readonly Dictionary<string, Exception> pushFailures = []; //by node id, thrown for every chunk
        private readonly Dictionary<SwarmHash, Exception> pushFailuresByChunk = [];
        private readonly List<PushingChunkRef> queue = [];
        private readonly Mock<IServiceScopeFactory> scopeFactoryMock = new();
        private readonly PushChunksBackgroundService service;

        // Constructor.
        public PushChunksBackgroundServiceTest()
        {
            // The queue: the search and the delete run over the list, the find-and-update claims a chunk ref
            // (the call with a sort) or charges it a failed attempt.
            var queueMock = new Mock<IRepository<PushingChunkRef, string>>();
            queueMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<PushingChunkRef>, Task<List<PostageBatchId>>>>(),
                    null))
                .Returns((Func<IQueryable<PushingChunkRef>, Task<List<PostageBatchId>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(queue)));
            queueMock.Setup(r => r.TryFindOneAndUpdateAsync(
                    It.IsAny<FilterDefinition<PushingChunkRef>>(),
                    It.IsAny<UpdateDefinition<PushingChunkRef>>(),
                    It.IsAny<FindOneAndUpdateOptions<PushingChunkRef>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    FilterDefinition<PushingChunkRef> filter,
                    UpdateDefinition<PushingChunkRef> _,
                    FindOneAndUpdateOptions<PushingChunkRef> options,
                    CancellationToken _) =>
                {
                    var chunkRef = queue
                        .Where(((ExpressionFilterDefinition<PushingChunkRef>)filter).Expression.Compile())
                        .OrderBy(r => r.CreationDateTime)
                        .FirstOrDefault();
                    if (chunkRef is not null && options.Sort is not null)
                        ReflectionHelper.SetValue(chunkRef, r => r.HandledDateTime, DateTime.UtcNow);
                    else if (chunkRef is not null)
                        ReflectionHelper.SetValue(chunkRef, r => r.FailedAttempts, chunkRef.FailedAttempts + 1);
                    return Task.FromResult(chunkRef);
                });
            queueMock.Setup(r => r.DeleteAsync(
                    It.IsAny<PushingChunkRef>(),
                    It.IsAny<FilterDefinition<PushingChunkRef>[]?>(),
                    It.IsAny<CancellationToken>()))
                .Callback((PushingChunkRef chunkRef, FilterDefinition<PushingChunkRef>[]? _, CancellationToken _) =>
                    queue.Remove(chunkRef))
                .Returns(Task.CompletedTask);
            queueMock.Setup(r => r.UpdateManyAsync(
                    It.IsAny<Expression<Func<PushingChunkRef, bool>>>(),
                    It.IsAny<UpdateDefinition<PushingChunkRef>>(),
                    It.IsAny<UpdateOptions?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    Expression<Func<PushingChunkRef, bool>> filter,
                    UpdateDefinition<PushingChunkRef> _,
                    UpdateOptions? _,
                    CancellationToken _) =>
                {
                    var abandoned = queue.Where(filter.Compile()).ToArray();
                    foreach (var chunkRef in abandoned) //the only bulk update of the queue: the abandon of a batch
                        ReflectionHelper.SetValue(chunkRef, r => r.FailedAttempts, 100);
                    return Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(abandoned.Length, abandoned.Length, null));
                });
            db.DbContextMock.Setup(c => c.ChunkPushQueue).Returns(queueMock.Object);

            // The nodes: each takes a chunk, or throws the failure a test gave it.
            foreach (var node in new[] { nodeA, nodeB })
                node.ClientMock.Setup(c => c.UploadChunkAsync(
                        It.IsAny<SwarmCac>(),
                        It.IsAny<PostageBatchId?>(),
                        It.IsAny<TagId?>(),
                        It.IsAny<PostageStamp?>(),
                        It.IsAny<CancellationToken>()))
                    .Returns((SwarmCac chunk, PostageBatchId? _, TagId? _, PostageStamp? _, CancellationToken _) =>
                    {
                        onPush?.Invoke();
                        if (pushFailures.TryGetValue(node.Instance.Id, out var nodeFailure))
                            return Task.FromException<SwarmHash>(nodeFailure);
                        if (pushFailuresByChunk.TryGetValue(chunk.Hash, out var chunkFailure))
                            return Task.FromException<SwarmHash>(chunkFailure);
                        pushedHashes.Add(chunk.Hash);
                        return Task.FromResult(chunk.Hash);
                    });
            nodeA.ClientMock.Setup(c => c.UploadSocAsync(
                    It.IsAny<SwarmSoc>(),
                    It.IsAny<PostageBatchId?>(),
                    It.IsAny<PostageStamp?>(),
                    It.IsAny<TagId?>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((SwarmSoc soc, PostageBatchId? _, PostageStamp? _, TagId? _, bool _, bool? _, CancellationToken _) =>
                {
                    pushedHashes.Add(soc.Hash);
                    return Task.FromResult(soc.Hash);
                });
            ownerNodes[BatchA] = nodeA.Instance;
            ownerNodes[BatchB] = nodeB.Instance;

            // The batches: locked when another instance holds them, each owned by its node.
            var postageBatchServiceMock = new Mock<IPostageBatchService>();
            postageBatchServiceMock.Setup(s => s.TryAcquireChunkPushLockAsync(It.IsAny<PostageBatchId>()))
                .ReturnsAsync((PostageBatchId batchId) =>
                    lockedBatches.Contains(batchId) ? null : new FakeResourceLockLease());
            postageBatchServiceMock.Setup(s => s.TryGetPostageBatchOwnerNodeAsync(It.IsAny<PostageBatchId>()))
                .ReturnsAsync((PostageBatchId batchId) => ownerNodes.GetValueOrDefault(batchId));

            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock.Setup(p => p.GetService(typeof(IBeehiveDbContext))).Returns(db.DbContext);
            serviceProviderMock.Setup(p => p.GetService(typeof(IPostageBatchService))).Returns(postageBatchServiceMock.Object);
            var scopeMock = new Mock<IServiceScope>();
            scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
            scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

            service = new PushChunksBackgroundService(
                new Mock<IBeeNodeLiveManager>().Object,
                logger,
                new Mock<ISerializerModifierAccessor>().Object,
                scopeFactoryMock.Object);
        }

        // Dispose.
        public void Dispose() => service.Dispose();

        // Tests.
        [Fact]
        public async Task AbandonsTheBatchItsNodeReportsGoneAndGoesOnWithTheOthers()
        {
            var refused = Enqueue(BatchA, 1);
            var sameBatch = Enqueue(BatchA, 2);
            var other = Enqueue(BatchB, 3);
            pushFailuresByChunk[refused.Hash] = ApiException(404);

            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None)); //no pause before the other batch
            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));
            Assert.False(await service.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([other.Hash], pushedHashes);
            Assert.Equal([refused, sameBatch], queue);
            Assert.All(queue, r => Assert.Equal(100, r.FailedAttempts));
        }

        [Fact]
        public async Task ChargesAChunkBeehiveCannotReadAndGoesOnWithItsBatch()
        {
            var unreadable = Enqueue(BatchA, 1);
            var next = Enqueue(BatchA, 2);
            db.Chunks.RemoveAll(c => c.Hash == unreadable.Hash); //and no node holds it

            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([next.Hash], pushedHashes);
            Assert.Equal([unreadable], queue);
            Assert.Equal(1, unreadable.FailedAttempts);
        }

        [Fact]
        public async Task ChargesTheChunkTheNodeRefusesAndGoesOnWithItsBatch()
        {
            var refused = Enqueue(BatchA, 1);
            var second = Enqueue(BatchA, 2);
            var third = Enqueue(BatchA, 3);
            pushFailuresByChunk[refused.Hash] = ApiException(400);

            var result = await service.TryProcessWorkAsync(CancellationToken.None);

            Assert.True(result);
            Assert.Equal([second.Hash, third.Hash], pushedHashes); //in the same cycle
            Assert.Equal([refused], queue);
            Assert.Equal(1, refused.FailedAttempts);
        }

        [Theory]
        [InlineData(null)] //the node doesn't answer
        [InlineData(500)]
        [InlineData(503)]
        public async Task DefersABatchWhoseNodeFailsWithoutChargingItsChunks(int? nodeStatusCode)
        {
            var first = Enqueue(BatchA, 1);
            var second = Enqueue(BatchA, 2);
            var other = Enqueue(BatchB, 3);
            pushFailures[nodeA.Instance.Id] = nodeStatusCode is null
                ? new HttpRequestException("Connection refused")
                : ApiException(nodeStatusCode.Value);

            // The batch of the failing node comes first, and stops at its first chunk.
            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));
            Assert.Empty(pushedHashes);
            Assert.Equal(0, first.FailedAttempts);

            // Its chunks wait in queue as they are, while the other batch is pushed.
            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));
            Assert.False(await service.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([other.Hash], pushedHashes);
            Assert.Equal([first, second], queue);
            Assert.All(queue, r => Assert.Equal(0, r.FailedAttempts));
            Assert.Null(second.HandledDateTime); //never claimed: the batch is not tried again before its time
            nodeA.ClientMock.Verify(c => c.UploadChunkAsync(
                It.IsAny<SwarmCac>(),
                It.IsAny<PostageBatchId?>(),
                It.IsAny<TagId?>(),
                It.IsAny<PostageStamp?>(),
                It.IsAny<CancellationToken>()), Times.Once);
            Assert.Contains(logger.Events, e =>
                e.Level == LogLevel.Warning && e.EventName == "PostageBatchPushesDeferredOnNodeFailure");
        }

        [Fact]
        public async Task LeavesAChunkWhosePushIsCancelledUnchargedAndLetsTheCancellationOut()
        {
            var cancelled = Enqueue(BatchA, 1);
            using var cancellation = new CancellationTokenSource();
            onPush = cancellation.Cancel; //a shutdown while the node is answering
            pushFailures[nodeA.Instance.Id] = new OperationCanceledException(cancellation.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.TryProcessWorkAsync(cancellation.Token));

            Assert.Equal([cancelled], queue);
            Assert.Equal(0, cancelled.FailedAttempts);
            Assert.DoesNotContain(logger.Events, e =>
                e.EventName is "FailedToPushChunk" or "PostageBatchPushesDeferredOnNodeFailure");
        }

        [Fact]
        public async Task LeavesInQueueTheChunksOfABatchWhoseOwnerNodeIsNotRegistered()
        {
            var orphan = Enqueue(BatchA, 1);
            var other = Enqueue(BatchB, 2);
            ownerNodes.Remove(BatchA);

            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));
            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));
            Assert.False(await service.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([other.Hash], pushedHashes);
            Assert.Equal([orphan], queue);
            Assert.Equal(0, orphan.FailedAttempts);
            Assert.Null(orphan.HandledDateTime);
            Assert.Single(logger.Events, e =>
                e.Level == LogLevel.Warning && e.EventName == "PostageBatchOwnerNodeNotFound");
        }

        [Fact]
        public async Task LeavesInQueueTheChunksOfANodeNotAliveWithoutCallingIt()
        {
            var waiting = Enqueue(BatchA, 1);
            nodeA.Instance.Status.FailedHeartbeatAttempt(["Node is not ready"], DateTime.UtcNow);

            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));
            Assert.False(await service.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([waiting], queue);
            Assert.Equal(0, waiting.FailedAttempts);
            Assert.Null(waiting.HandledDateTime);
            Assert.Empty(nodeA.ClientMock.Invocations);
        }

        [Fact]
        public async Task LeavesUntouchedTheChunkRefsOfABatchAnotherInstanceIsPushing()
        {
            var held = Enqueue(BatchA, 1);
            var free = Enqueue(BatchB, 2);
            lockedBatches.Add(BatchA);

            var result = await service.TryProcessWorkAsync(CancellationToken.None);

            Assert.True(result);
            Assert.Equal([free.Hash], pushedHashes);
            Assert.Equal([held], queue);
            Assert.Null(held.HandledDateTime); //free to be claimed at once by who holds its batch
            Assert.Equal(0, held.FailedAttempts);
        }

        [Fact]
        public async Task LogsTheChunkAbandonedAtItsLastFailedAttemptAndLeavesItInQueue()
        {
            var lastAttempt = Enqueue(BatchA, 1, failedAttempts: 99);
            var earlierAttempt = Enqueue(BatchA, 2, failedAttempts: 98);
            pushFailuresByChunk[lastAttempt.Hash] = ApiException(400);
            pushFailuresByChunk[earlierAttempt.Hash] = ApiException(400);

            await service.TryProcessWorkAsync(CancellationToken.None);

            Assert.Equal(100, lastAttempt.FailedAttempts);
            Assert.Equal(99, earlierAttempt.FailedAttempts);
            var abandoned = Assert.Single(logger.Events, e => e.EventName == "ChunkPushAbandoned");
            Assert.Equal(LogLevel.Error, abandoned.Level);
            Assert.Contains(lastAttempt.Hash.ToString(), abandoned.Message, StringComparison.Ordinal);

            // An abandoned chunk ref stays in queue and is not pushed anymore, whatever waits with it.
            ReflectionHelper.SetValue(lastAttempt, r => r.HandledDateTime, null);
            var next = Enqueue(BatchA, 3);

            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));
            Assert.False(await service.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([next.Hash], pushedHashes);
            Assert.Equal(100, lastAttempt.FailedAttempts);
            Assert.Equal([lastAttempt, earlierAttempt], queue);
        }

        [Fact]
        public async Task PushesAChunkAgainOnlyAfterItsRetryTime()
        {
            var justFailed = Enqueue(BatchA, 1, failedAttempts: 1, handledDateTime: DateTime.UtcNow.AddSeconds(-30));
            var readyAgain = Enqueue(BatchA, 2, failedAttempts: 1, handledDateTime: DateTime.UtcNow.AddSeconds(-61));

            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));
            Assert.False(await service.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([readyAgain.Hash], pushedHashes);
            Assert.Equal([justFailed], queue);
        }

        [Fact]
        public async Task PushesADeferredBatchAgainOnceItsRetryTimeIsPast()
        {
            var waiting = Enqueue(BatchA, 1);
            nodeA.Instance.Status.FailedHeartbeatAttempt(["Node is not ready"], DateTime.UtcNow);
            using var quickService = new PushChunksBackgroundService(
                new Mock<IBeeNodeLiveManager>().Object,
                logger,
                new Mock<ISerializerModifierAccessor>().Object,
                scopeFactoryMock.Object)
            {
                RetryBatchAfter = TimeSpan.Zero
            };

            Assert.True(await quickService.TryProcessWorkAsync(CancellationToken.None)); //deferred
            nodeA.Instance.Status.SucceededHeartbeatAttempt(DateTime.UtcNow); //the node is back
            Assert.True(await quickService.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([waiting.Hash], pushedHashes);
            Assert.Empty(queue);
        }

        [Fact]
        public async Task PushesASingleOwnerChunk()
        {
            var signer = new PrivateKeySigner(new EthPrivateKey(PrivateKeyHex));
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, Enumerable.Range(0, 42).Select(i => (byte)i).ToArray()).SpanData;
            SwarmSocIdentifier identifier = Enumerable.Range(0, SwarmSocIdentifier.IdentifierSize).Select(i => (byte)i).ToArray();
            var soc = new SwarmSoc(identifier, signer.PublicAddress, new SwarmCac(new SwarmChunkBmt().Hash(spanData), spanData));
            soc.Sign(signer, new Hasher());
            db.AddChunk(new Chunk(soc.Hash, soc.GetFullPayload(), true));
            var socRef = new PushingChunkRef(soc.Hash, BatchA);
            ReflectionHelper.SetValue(socRef, r => r.Id, "ref-soc");
            queue.Add(socRef);

            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([soc.Hash], pushedHashes);
            Assert.Empty(queue);
        }

        [Fact]
        public async Task PushesTheChunksOfABatchInQueueOrderAndDequeuesThem()
        {
            var first = Enqueue(BatchA, 1);
            var second = Enqueue(BatchA, 2);

            Assert.True(await service.TryProcessWorkAsync(CancellationToken.None));
            Assert.False(await service.TryProcessWorkAsync(CancellationToken.None));

            Assert.Equal([first.Hash, second.Hash], pushedHashes);
            Assert.Empty(queue);
        }

        // Helpers.
        private static SwarmSdkApiException ApiException(int statusCode) =>
            new($"The HTTP status code of the response was not expected ({statusCode}).",
                statusCode,
                "",
                new Dictionary<string, IEnumerable<string>>(),
                null);

        /// <summary>
        /// Queue a chunk beehive holds, after the ones already queued
        /// </summary>
        private PushingChunkRef Enqueue(
            PostageBatchId batchId,
            byte seed,
            int failedAttempts = 0,
            DateTime? handledDateTime = null)
        {
            var spanData = SwarmCac.BuildFromData(
                SwarmHash.Zero, Enumerable.Range(0, 42).Select(i => (byte)(i + seed)).ToArray()).SpanData;
            var chunk = new SwarmCac(new SwarmChunkBmt().Hash(spanData), spanData);
            db.AddChunk(new Chunk(chunk.Hash, chunk.GetFullPayload(), false));

            var chunkRef = new PushingChunkRef(chunk.Hash, batchId);
            ReflectionHelper.SetValue(chunkRef, r => r.Id, $"ref-{queue.Count}");
            ReflectionHelper.SetValue(chunkRef, r => r.CreationDateTime, FirstQueuedTime.AddSeconds(queue.Count));
            ReflectionHelper.SetValue(chunkRef, r => r.FailedAttempts, failedAttempts);
            ReflectionHelper.SetValue(chunkRef, r => r.HandledDateTime, handledDateTime);
            queue.Add(chunkRef);

            return chunkRef;
        }
    }
}

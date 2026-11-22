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
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Background
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public class PushChunksBackgroundService(
        IBeeNodeLiveManager beeNodeLiveManager,
        ILogger<PushChunksBackgroundService> logger,
        ISerializerModifierAccessor serializerModifierAccessor,
        IServiceScopeFactory serviceScopeFactory)
        : BackgroundService
    {
        // Consts.
        private const int MaxFailedAttempts = 100;
        
        // Fields.
        private readonly Dictionary<PostageBatchId, DateTime> deferredBatches = []; //with the time each can be tried again
        private readonly TimeSpan processInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan retryChunkAfter = TimeSpan.FromSeconds(60);

        // Properties.
        /// <summary>
        /// How long a postage batch whose owner node can't take its chunks waits before it is tried again
        /// </summary>
        internal TimeSpan RetryBatchAfter { get; init; } = TimeSpan.FromSeconds(60);

        // Internal methods.
        /// <summary>
        /// Push to its owner node the queued chunks of the first postage batch free to be pushed
        /// </summary>
        /// <returns>False when no chunk waits to be pushed</returns>
        internal async Task<bool> TryProcessWorkAsync(
            CancellationToken cancellationToken)
        {
            // Initialize scope.
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IBeehiveDbContext>();
            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext, logger, serializerModifierAccessor);
            var postageBatchService = scope.ServiceProvider.GetRequiredService<IPostageBatchService>();

            // Open the ambient context scrinium requires outside a request: the db operations of this cycle.
            using var dbExecutionContext = new DbExecutionContextHandler(dbContext);

            // Search a postage batch with chunks to push, and lock it. The search only reads: a chunk ref is claimed
            // under the lock of its batch, so the refs of a batch another instance is pushing stay as they are.
            var now = DateTime.UtcNow;
            foreach (var deferredBatchId in deferredBatches.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                deferredBatches.Remove(deferredBatchId);
            List<PostageBatchId> skipBatchIds = [.. deferredBatches.Keys];
            PostageBatchId batchId = default;
            IResourceLockLease? lockLease = null;
            while (lockLease == null)
            {
                var retryBefore = DateTime.UtcNow - retryChunkAfter;
                var nextBatchIds = await dbContext.ChunkPushQueue.QueryElementsAsync(chunkRefs =>
                    chunkRefs.Where(r =>
                            !skipBatchIds.Contains(r.BatchId) &&
                            (r.HandledDateTime == null || r.HandledDateTime <= retryBefore) &&
                            r.FailedAttempts < MaxFailedAttempts)
                        .OrderBy(r => r.CreationDateTime)
                        .Select(r => r.BatchId)
                        .Take(1)
                        .ToListAsync(cancellationToken: cancellationToken));
                if (nextBatchIds.Count == 0)
                {
                    logger.NoChunksToPush();
                    return false;
                }

                // Try to lock on postage batch to push chunks.
                batchId = nextBatchIds[0];
                lockLease = await postageBatchService.TryAcquireChunkPushLockAsync(batchId);
                if (lockLease is null)
                    skipBatchIds.Add(batchId);
            }

            await using (lockLease)
            {
                // Identify the node owner of the postage batch. Without a node able to take them the chunk refs stay
                // in queue with their attempts, and the batch is tried again later: a node can be away for long.
                var ownerNode = await postageBatchService.TryGetPostageBatchOwnerNodeAsync(batchId);
                if (ownerNode is null)
                {
                    logger.PostageBatchOwnerNodeNotFound(batchId);
                    deferredBatches[batchId] = DateTime.UtcNow + RetryBatchAfter;
                    return true;
                }
                if (ownerNode.Status.NotAliveReason is not null)
                {
                    logger.PostageBatchPushesDeferredOnNodeNotAlive(batchId, ownerNode.Id);
                    deferredBatches[batchId] = DateTime.UtcNow + RetryBatchAfter;
                    return true;
                }

                // Push the chunks enqueued with the postage batch, until one says the batch can't go on.
                // This helps to reduce the postage batch lookup on the owner node.
                while (true)
                {
                    var claimTime = DateTime.UtcNow;
                    var retryBefore = claimTime - retryChunkAfter;
                    var chunkRef = await dbContext.ChunkPushQueue.TryFindOneAndUpdateAsync(
                        new ExpressionFilterDefinition<PushingChunkRef>(r =>
                            r.BatchId == batchId &&
                            (r.HandledDateTime == null || r.HandledDateTime <= retryBefore) &&
                            r.FailedAttempts < MaxFailedAttempts),
                        Builders<PushingChunkRef>.Update.Set(r => r.HandledDateTime, claimTime),
                        new FindOneAndUpdateOptions<PushingChunkRef>
                        {
                            Sort = Builders<PushingChunkRef>.Sort.Ascending(r => r.CreationDateTime)
                        },
                        cancellationToken);
                    if (chunkRef is null)
                        return true;

                    if (!await TryPushChunkAsync(chunkStore, chunkRef, dbContext, ownerNode, cancellationToken))
                        return true;
                }
            }
        }

        // Protected methods.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.PushChunksBackgroundServiceStarted();

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    while (await TryProcessWorkAsync(stoppingToken)) { }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    logger.UnhandledExceptionPushingChunksToNode(ex);
                }

                if (!stoppingToken.IsCancellationRequested)
                    await Task.Delay(processInterval, stoppingToken);
            }

            logger.PushChunksBackgroundServiceStopped();
        }

        // Helpers.
        private async Task AbandonPostageBatchPushesAsync(
            PostageBatchId batchId,
            IBeehiveDbContext dbContext,
            CancellationToken cancellationToken)
        {
            //exhausting their attempts at once leaves the chunk refs in the final state of any failed push
            var result = await dbContext.ChunkPushQueue.UpdateManyAsync(
                r => r.BatchId == batchId && r.FailedAttempts < MaxFailedAttempts,
                Builders<PushingChunkRef>.Update.Set(r => r.FailedAttempts, MaxFailedAttempts),
                cancellationToken: cancellationToken);

            logger.AbandonedPostageBatchChunks(batchId, result.ModifiedCount);
        }

        /// <returns>False when the postage batch can't be pushed further in this cycle</returns>
        private async Task<bool> TryPushChunkAsync(
            BeehiveChunkStore chunkStore,
            PushingChunkRef chunkRef,
            IBeehiveDbContext dbContext,
            BeeNodeLiveInstance ownerNode,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get actual chunk.
                var chunk = await chunkStore.GetAsync(chunkRef.Hash, cancellationToken: cancellationToken);
                if (chunk is not (SwarmCac or SwarmSoc))
                    throw new InvalidOperationException("Unknown chunk type");

                // Push chunk to node.
                try
                {
                    if (chunk is SwarmCac cac)
                    {
                        await ownerNode.Client.UploadChunkAsync(
                            cac,
                            chunkRef.BatchId,
                            cancellationToken: cancellationToken);

                        logger.SucceededToPushCac(chunkRef.Hash);
                    }
                    else
                    {
                        await ownerNode.Client.UploadSocAsync(
                            (SwarmSoc)chunk,
                            chunkRef.BatchId,
                            cancellationToken: cancellationToken);

                        logger.SucceededToPushSoc(chunkRef.Hash);
                    }
                }
                catch (Exception e) when (
                    e is not SwarmSdkApiException { StatusCode: < 500 } &&
                    !cancellationToken.IsCancellationRequested)
                {
                    // The node didn't answer, or failed on its own: nothing says the chunk is at fault. Its ref keeps
                    // its attempts, and the batch is tried again later.
                    logger.PostageBatchPushesDeferredOnNodeFailure(chunkRef.BatchId, ownerNode.Id, chunkRef.Hash, e);
                    deferredBatches[chunkRef.BatchId] = DateTime.UtcNow + RetryBatchAfter;

                    return false;
                }

                // Dequeue chunk ref.
                await dbContext.ChunkPushQueue.DeleteAsync(chunkRef, cancellationToken: cancellationToken);

                return true;
            }
            catch (SwarmSdkApiException e) when (e.StatusCode == 404)
            {
                logger.PostageBatchNotFound(chunkRef.BatchId);
                logger.FailedToPushChunk(chunkRef.Hash, e);

                // The node's answer: the batch is gone. Abandon its queued chunks, no retry can push them anymore.
                await AbandonPostageBatchPushesAsync(chunkRef.BatchId, dbContext, cancellationToken);

                return false;
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                // The failure of this chunk alone: the cycle goes on with the next ones of its batch.
                logger.FailedToPushChunk(chunkRef.Hash, e);

                var failedChunkRef = await dbContext.ChunkPushQueue.TryFindOneAndUpdateAsync(
                    new ExpressionFilterDefinition<PushingChunkRef>(r => r.Id == chunkRef.Id),
                    Builders<PushingChunkRef>.Update.Inc(r => r.FailedAttempts, 1),
                    new FindOneAndUpdateOptions<PushingChunkRef> { ReturnDocument = ReturnDocument.After },
                    cancellationToken: cancellationToken);
                if (failedChunkRef?.FailedAttempts >= MaxFailedAttempts)
                    logger.ChunkPushAbandoned(chunkRef.Hash, chunkRef.BatchId, failedChunkRef.FailedAttempts);

                return true;
            }
        }
    }
}

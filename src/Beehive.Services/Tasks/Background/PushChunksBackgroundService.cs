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
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.BeeNet.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core.Serialization.Modifiers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Background
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public class PushChunksBackgroundService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        ILogger<PushChunksBackgroundService> logger,
        IPostageBatchService postageBatchService,
        ISerializerModifierAccessor serializerModifierAccessor)
        : BackgroundService
    {
        // Consts.
        private const int MaxFailedAttempts = 100;
        private readonly TimeSpan ProcessInterval = TimeSpan.FromMilliseconds(1000);
        private readonly TimeSpan RetryChunkAfter = TimeSpan.FromSeconds(60);
        
        // Protected methods.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.PushChunksBackgroundServiceStarted();

            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext, serializerModifierAccessor);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessWorkAsync(chunkStore, stoppingToken);
                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    logger.UnhandledExceptionPushingChunksToNode(ex);
                }

                if (!stoppingToken.IsCancellationRequested)
                    await Task.Delay(ProcessInterval, stoppingToken);
            }
            
            logger.PushChunksBackgroundServiceStopped();
        }

        // Helpers.
        private async Task ProcessWorkAsync(
            BeehiveChunkStore chunkStore,
            CancellationToken cancellationToken)
        {
            // Get the initial chunk ref.
            var now = DateTime.UtcNow;
            var chunkRef = await TryFindNextChunkRefAsync(null, now, cancellationToken);
            if (chunkRef is null)
            {
                logger.NoChunksToPush();
                return;   
            }

            // Identify the node owner of the postage batch.
            var ownerNode = await postageBatchService.TryGetPostageBatchOwnerNodeAsync(chunkRef.BatchId);
            if (ownerNode is null)
            {
                logger.PostageBatchOwnerNodeNotFound(chunkRef.BatchId);
                logger.FailedToPushChunk(chunkRef.Hash, null);

                // Because this error involves all the chunks with the same postage batch,
                // report the error on all of them.
                await dbContext.ChunkPushQueue.UpdateManyAsync(r => r.BatchId == chunkRef.BatchId,
                    Builders<PushingChunkRef>.Update.Combine(
                        Builders<PushingChunkRef>.Update.Inc(r => r.FailedAttempts, 1),
                        Builders<PushingChunkRef>.Update.Set(r => r.HandledDateTime, DateTime.UtcNow)),
                    cancellationToken: cancellationToken);

                return;
            }

            // Try to push chunk.
            if (!await TryPushChunkAsync(chunkStore, chunkRef, ownerNode, cancellationToken))
                return;
            
            // While pushes succeed, try to process also all other chunks enqueued with the same postage batch.
            // This helps to reduce the postage batch lookup on the owner node.
            while (true)
            {
                chunkRef = await TryFindNextChunkRefAsync(chunkRef.BatchId, DateTime.UtcNow, cancellationToken);
                if (chunkRef is null ||
                    !await TryPushChunkAsync(chunkStore, chunkRef, ownerNode, cancellationToken))
                    return;
            }
        }

        private async Task<bool> TryPushChunkAsync(
            BeehiveChunkStore chunkStore,
            PushingChunkRef chunkRef,
            BeeNodeLiveInstance ownerNode,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get actual chunk.
                var chunk = await chunkStore.GetAsync(chunkRef.Hash, cancellationToken: cancellationToken);

                // Push chunk to node.
                switch (chunk)
                {
                    case SwarmCac cac:
                        await ownerNode.Client.UploadChunkAsync(
                            cac,
                            chunkRef.BatchId,
                            cancellationToken: cancellationToken);
                        break;
                    case SwarmSoc soc:
                        await ownerNode.Client.UploadSocAsync(
                            soc,
                            chunkRef.BatchId,
                            cancellationToken: cancellationToken);
                        break;
                    default: throw new InvalidOperationException("Unknown chunk type");
                }
                
                return true;
            }
            catch (Exception e)
            {
                logger.FailedToPushChunk(chunkRef.Hash, e);

                await dbContext.ChunkPushQueue.FindOneAndUpdateAsync(
                    new ExpressionFilterDefinition<PushingChunkRef>(r => r.Id == chunkRef.Id),
                    Builders<PushingChunkRef>.Update.Inc(r => r.FailedAttempts, 1),
                    new FindOneAndUpdateOptions<PushingChunkRef>(),
                    cancellationToken: cancellationToken);

                return false;
            }
        }

        private async Task<PushingChunkRef?> TryFindNextChunkRefAsync(
            PostageBatchId? filterOnBatchId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var filter = filterOnBatchId.HasValue
                ? new ExpressionFilterDefinition<PushingChunkRef>(r =>
                    r.BatchId == filterOnBatchId &&
                    (r.HandledDateTime == null || r.HandledDateTime <= now - RetryChunkAfter) &&
                    r.FailedAttempts < MaxFailedAttempts)
                : new ExpressionFilterDefinition<PushingChunkRef>(r =>
                    (r.HandledDateTime == null || r.HandledDateTime <= now - RetryChunkAfter) &&
                    r.FailedAttempts < MaxFailedAttempts);
            
            return await dbContext.ChunkPushQueue.FindOneAndUpdateAsync(
                filter,
                Builders<PushingChunkRef>.Update.Set(r => r.HandledDateTime, now),
                new FindOneAndUpdateOptions<PushingChunkRef>
                {
                    Sort = Builders<PushingChunkRef>.Sort.Ascending(r => r.CreationDateTime)
                },
                cancellationToken);
        }
    }
}
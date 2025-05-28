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
using Etherna.BeeNet.Exceptions;
using Etherna.BeeNet.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core.Serialization.Modifiers;
using Etherna.MongODM.Core.Utility;
using Microsoft.Extensions.DependencyInjection;
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
        ISerializerModifierAccessor serializerModifierAccessor,
        IServiceScopeFactory serviceScopeFactory)
        : BackgroundService
    {
        // Consts.
        private const int MaxFailedAttempts = 100;
        
        // Fields.
        private IPostageBatchService postageBatchService = null!;
        private readonly TimeSpan processInterval = TimeSpan.FromSeconds(5);
        private readonly TimeSpan retryChunkAfter = TimeSpan.FromSeconds(60);
        
        // Protected methods.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.PushChunksBackgroundServiceStarted();

            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext, serializerModifierAccessor);
            using var scope = serviceScopeFactory.CreateScope();
            postageBatchService = scope.ServiceProvider.GetRequiredService<IPostageBatchService>();
            using var _ = new DbExecutionContextHandler(dbContext);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    while (await TryProcessWorkAsync(chunkStore, stoppingToken)) { }
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
        private async Task ReportPostageBatchUploadFailureAsync(
            PostageBatchId batchId,
            CancellationToken cancellationToken)
        {
            await dbContext.ChunkPushQueue.UpdateManyAsync(r => r.BatchId == batchId,
                Builders<PushingChunkRef>.Update.Combine(
                    Builders<PushingChunkRef>.Update.Inc(r => r.FailedAttempts, 1),
                    Builders<PushingChunkRef>.Update.Set(r => r.HandledDateTime, DateTime.UtcNow)),
                cancellationToken: cancellationToken);
        }
        
        private async Task<bool> TryProcessWorkAsync(
            BeehiveChunkStore chunkStore,
            CancellationToken cancellationToken)
        {
            // Get the initial chunk ref.
            var now = DateTime.UtcNow;
            var chunkRef = await TryFindNextChunkRefAsync(null, now, cancellationToken);
            if (chunkRef is null)
            {
                logger.NoChunksToPush();
                return false;   
            }

            // Identify the node owner of the postage batch.
            var ownerNode = await postageBatchService.TryGetPostageBatchOwnerNodeAsync(chunkRef.BatchId);
            if (ownerNode is null)
            {
                logger.PostageBatchOwnerNodeNotFound(chunkRef.BatchId);
                logger.FailedToPushChunk(chunkRef.Hash, null);

                // Because this error involves all the chunks with the same postage batch,
                // report the error on all of them.
                await ReportPostageBatchUploadFailureAsync(chunkRef.BatchId, cancellationToken);

                return false;
            }

            // Try to push chunk.
            if (!await TryPushChunkAsync(chunkStore, chunkRef, ownerNode, cancellationToken))
                return false;
            
            // While pushes succeed, try to process also all other chunks enqueued with the same postage batch.
            // This helps to reduce the postage batch lookup on the owner node.
            while (true)
            {
                chunkRef = await TryFindNextChunkRefAsync(chunkRef.BatchId, DateTime.UtcNow, cancellationToken);
                
                if (chunkRef is null)
                    return true;

                if (!await TryPushChunkAsync(chunkStore, chunkRef, ownerNode, cancellationToken))
                    return false;
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

                        logger.SucceededToPushCac(chunkRef.Hash);
                        break;
                    case SwarmSoc soc:
                        await ownerNode.Client.UploadSocAsync(
                            soc,
                            chunkRef.BatchId,
                            cancellationToken: cancellationToken);

                        logger.SucceededToPushSoc(chunkRef.Hash);
                        break;
                    default: throw new InvalidOperationException("Unknown chunk type");
                }

                // Dequeue chunk ref.
                await dbContext.ChunkPushQueue.DeleteAsync(chunkRef, cancellationToken: cancellationToken);

                return true;
            }
            catch (BeeNetApiException e) when (e.StatusCode == 404)
            {
                logger.PostageBatchNotFound(chunkRef.BatchId);
                logger.FailedToPushChunk(chunkRef.Hash, e);

                await ReportPostageBatchUploadFailureAsync(chunkRef.BatchId, cancellationToken);

                return false;
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
                    (r.HandledDateTime == null || r.HandledDateTime <= now - retryChunkAfter) &&
                    r.FailedAttempts < MaxFailedAttempts)
                : new ExpressionFilterDefinition<PushingChunkRef>(r =>
                    (r.HandledDateTime == null || r.HandledDateTime <= now - retryChunkAfter) &&
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
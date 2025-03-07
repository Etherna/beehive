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
using Etherna.Beehive.Domain.Exceptions;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Models;
using Etherna.MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public class PostageBatchService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        IResourceLockService resourceLockService)
        : IPostageBatchService
    {
        // Methods.
        public async Task<ResourceLockHandler<PostageBatchLock>> AcquireLockAsync(
            PostageBatchId batchId,
            bool exclusiveAccess)
        {
            var handler = await resourceLockService.TryAcquireLockAsync(
                () => new PostageBatchLock(batchId, exclusiveAccess),
                dbContext.PostageBatchLocks,
                batchId.ToString(),
                exclusiveAccess);
            
            if (handler is null)
                throw new ResourceLockException();

            return handler;
        }

        public async Task IncrementPostageBucketsCacheAsync(
            PostageBucketsCache prevStatus,
            PostageBuckets currentStatus)
        {
            ArgumentNullException.ThrowIfNull(prevStatus, nameof(prevStatus));
            ArgumentNullException.ThrowIfNull(currentStatus, nameof(currentStatus));
            
            // Calculate increments.
            var bucketsIncrement = prevStatus.BucketsCollisions.Zip(currentStatus.GetBuckets())
                .Select(pair => pair.Second - pair.First)
                .ToArray();
            
            // Build filter.
            //filter batch id
            var filters = new List<FilterDefinition<PostageBucketsCache>>
            {
                Builders<PostageBucketsCache>.Filter.Eq(m => m.BatchId, prevStatus.BatchId)
            };

            //verify upper bounds
            for (int i = 0; i < bucketsIncrement.Length; i++)
                filters.Add(Builders<PostageBucketsCache>.Filter.Lte(
                    $"{nameof(PostageBucketsCache.BucketsCollisions)}.{i}",
                    prevStatus.UpperBound - bucketsIncrement[i]));
            
            // Build updates.
            var updates = new List<UpdateDefinition<PostageBucketsCache>>();
            for (int i = 0; i < bucketsIncrement.Length; i++)
                if (bucketsIncrement[i] != 0)
                    updates.Add(Builders<PostageBucketsCache>.Update.Inc(
                        $"{nameof(PostageBucketsCache.BucketsCollisions)}.{i}",
                        bucketsIncrement[i]));

            // Exec update.
            await dbContext.PostageBucketsCache.FindOneAndUpdateAsync(
                Builders<PostageBucketsCache>.Filter.And(filters),
                Builders<PostageBucketsCache>.Update.Combine(updates),
                new FindOneAndUpdateOptions<PostageBucketsCache>());
        }
        
        public Task<bool> IsLockedAsync(PostageBatchId batchId) =>
            resourceLockService.IsLockedAsync(
                dbContext.PostageBatchLocks,
                batchId.ToString());

        public async Task<PostageBucketsCache?> TryGetPostageBucketsAsync(
            PostageBatchId batchId,
            bool forceRefreshCache = false)
        {
            // Try load existing cache from db.
            var bucketsCache = await dbContext.PostageBucketsCache.TryFindOneAsync(b => b.BatchId == batchId);
            
            // Try load status from node.
            if (bucketsCache == null || forceRefreshCache)
            {
                // Remove cached value from db.
                if (bucketsCache != null)
                    await dbContext.PostageBucketsCache.DeleteAsync(bucketsCache);
                
                // Get fresh value.
                var nodeLiveInstance = beeNodeLiveManager.TryGetPostageBatchOwnerNode(batchId);
                if (nodeLiveInstance == null) //postage doesn't exist
                    return null;
                var (bucketsLiveCollisions, upperBound) = await nodeLiveInstance.GetPostageBatchBucketsCollisionsAsync(batchId);
                
                // Cache new value on db.
                bucketsCache = new PostageBucketsCache(
                    batchId,
                    bucketsLiveCollisions.ToArray(),
                    nodeLiveInstance.Id,
                    upperBound);
                await dbContext.PostageBucketsCache.CreateAsync(bucketsCache);
            }

            return bucketsCache;
        }
    }
}
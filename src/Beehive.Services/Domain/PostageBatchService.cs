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
        IBeehiveDbContext dbContext)
        : IPostageBatchService
    {
        // Methods.
        public async Task<bool> AcquireLockAsync(
            PostageBatchId batchId,
            bool exclusiveAccess)
        {
            /*
             * Requirements:
             * - Different batch Ids never collides, and concurrent acquisitions are always possible
             * - On same batchId any number of locks are possible without exclusive access
             * - If any alive locks exists on a batchId, new exclusive access on the same batchId will fail
             * - If a lock with exclusive access exists on a batchId, any other lock access will fail
             *
             * Solution:
             * - Index with unique restriction on batchId is created
             * - A exclusive lock simply tries to create a new lock
             * - A not exclusive lock tries to exec atomic findAndUpdate with upsert, incrementing a counter.
             *   The release will decrement the counter
             */

            var now = DateTime.UtcNow;

            // If prev lock exists, verify if is still valid, and if new one or old one are exclusive.
            var prevLock = await dbContext.PostageBatchLocks.TryFindOneAsync(l => l.BatchId == batchId);
            if (prevLock?.ExpirationTime > now && (exclusiveAccess || prevLock.ExclusiveAccess))
                return false;
            
            // Delete old expired lock, if exists.
            // Additional check on expiration because concurrent access could have renewed it.
            if (prevLock is not null)
            {
                try
                {
                    await dbContext.PostageBatchLocks.DeleteAsync(
                        prevLock,
                        [Builders<PostageBatchLock>.Filter.Lte(l => l.ExpirationTime, now)]);
                }
                catch (MongoWriteException) { }
            }

            // Create or upsert new lock. It could still fail here because of concurrent accesses.
            // In that case unique index on chunkPinId permits only one to proceed.
            try
            {
                if (exclusiveAccess)
                {
                    var batchLock = new PostageBatchLock(batchId, exclusiveAccess);
                    await dbContext.PostageBatchLocks.CreateAsync(batchLock);
                }
                else
                {
                    await dbContext.PostageBatchLocks.UpsertAsync(
                        Builders<PostageBatchLock>.Filter.And(
                            Builders<PostageBatchLock>.Filter.Eq(l => l.BatchId, batchId),
                            Builders<PostageBatchLock>.Filter.Eq(l => l.ExclusiveAccess,
                                false)), //don't update if ExclusiveAccess == true
                        Builders<PostageBatchLock>.Update.Combine(
                            Builders<PostageBatchLock>.Update.Inc(l => l.LockCounter, 1), //increment counter
                            Builders<PostageBatchLock>.Update.Set(l => l.ExpirationTime, //renew expiration time
                                now.Add(PostageBatchLock.LockDuration))),
                        new PostageBatchLock(batchId, exclusiveAccess),
                        [
                            nameof(PostageBatchLock.ExpirationTime),
                            nameof(PostageBatchLock.LockCounter)
                        ]);
                }
            }
            catch (Exception e) when (e is MongoCommandException
                                        or MongoWriteException)
            {
                // Catch any unique index collision.
                return false;
            }

            return true;
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
        
        public async Task<bool> IsLockedAsync(PostageBatchId batchId)
        {
            var batchLock = await dbContext.PostageBatchLocks.TryFindOneAsync(l => l.BatchId == batchId);
            return batchLock != null && batchLock.ExpirationTime > DateTime.UtcNow;
        }
        
        public async Task<bool> ReleaseLockAsync(
            PostageBatchId batchId,
            bool exclusiveAccess)
        {
            // Find lock and optionally decrease counter.
            var batchLock = exclusiveAccess ?
                
                //exclusive access
                await dbContext.PostageBatchLocks.TryFindOneAsync(
                    l => l.BatchId == batchId && l.ExclusiveAccess) :
                
                //not exclusive access
                await dbContext.PostageBatchLocks.FindOneAndUpdateAsync(
                    Builders<PostageBatchLock>.Filter.And(
                        Builders<PostageBatchLock>.Filter.Eq(l => l.BatchId, batchId),
                        Builders<PostageBatchLock>.Filter.Eq(l => l.ExclusiveAccess, false)),
                    Builders<PostageBatchLock>.Update.Inc(l => l.LockCounter, -1),
                    new FindOneAndUpdateOptions<PostageBatchLock>
                    {
                        ReturnDocument = ReturnDocument.After
                    });
            
            // If lock not found.
            if (batchLock is null)
                return false;
            
            // Try delete document.
            if (batchLock.LockCounter == 0)
            {
                List<FilterDefinition<PostageBatchLock>> additionalFilters =
                    [Builders<PostageBatchLock>.Filter.Eq(l => l.ExclusiveAccess, exclusiveAccess)];
                if (!exclusiveAccess)
                    additionalFilters.Add(Builders<PostageBatchLock>.Filter.Eq(l => l.LockCounter, 0));
                
                await dbContext.PostageBatchLocks.DeleteAsync(
                    batchLock,
                    additionalFilters.ToArray());
            }

            return true;
        }

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
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
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.BeeNet.Hashing.Postage;
using Etherna.BeeNet.Models;
using Etherna.MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PostageStamp = Etherna.Beehive.Domain.Models.PostageStamp;

namespace Etherna.Beehive.Services.Domain
{
    public sealed class PostageBatchService(
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
                throw new ResourceLockException($"Failed to lock batch Id '{batchId}'");

            return handler;
        }

        public async Task<(PostageBatchId BatchId, EthTxHash TxHash)> BuyPostageBatchAsync(
            BzzBalance amount,
            int depth,
            string? label,
            bool immutable,
            ulong? gasLimit,
            XDaiBalance? gasPrice)
        {
            // Select node.
            var beeNodeInstance = await beeNodeLiveManager.TrySelectHealthyNodeAsync(
                BeeNodeSelectionMode.RoundRobin,
                "buyPostageBatch",
                node => Task.FromResult(node.IsBatchCreationEnabled));

            if (beeNodeInstance is null)
                throw new InvalidOperationException("No healthy nodes available for batch creation");

            // Buy postage.
            var (batchId, txHash) = await beeNodeInstance.BuyPostageBatchAsync(
                amount,
                depth,
                label,
                immutable,
                gasLimit,
                gasPrice);
            
            // Store postage batch cache.
            var batchCache = new PostageBatchCache(
                batchId,
                new uint[PostageBuckets.BucketsSize],
                depth,
                immutable,
                beeNodeInstance.Id);
            await dbContext.PostageBatchesCache.CreateAsync(batchCache);
            
            return (batchId, txHash);
        }

        public async Task<EthTxHash> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            ulong? gasLimit,
            XDaiBalance? gasPrice)
        {
            // Acquire lock on postage batch.
            await using var batchLockHandler = await AcquireLockAsync(batchId, true);
            
            // Try get cached postage batch.
            var postageCache = await TryGetPostageBatchCacheAsync(batchId);
            if (postageCache is null)
                throw new KeyNotFoundException();
            
            // Dilute on node.
            var node = beeNodeLiveManager.TryGetPostageBatchOwnerNode(batchId);
            if (node == null)
                throw new KeyNotFoundException();
            var txHash = await node.DilutePostageBatchAsync(batchId, depth, gasLimit, gasPrice);
            
            // Update cache.
            postageCache.Depth = depth;
            await dbContext.SaveChangesAsync();
            
            return txHash;
        }

        public Task<bool> IsLockedAsync(PostageBatchId batchId) =>
            resourceLockService.IsLockedAsync(
                dbContext.PostageBatchLocks,
                batchId.ToString());

        public async Task StoreStampedChunksAsync(
            PostageBatchCache postageBatchCache,
            HashSet<SwarmHash> stampedChunkHashesCache,
            IPostageStamper newPostageStamper)
        {
            ArgumentNullException.ThrowIfNull(postageBatchCache, nameof(postageBatchCache));
            ArgumentNullException.ThrowIfNull(newPostageStamper, nameof(newPostageStamper));
            
            // Add new postage stamps.
            var newPostageStamps = newPostageStamper.StampStore.GetItems()
                .Where(s => !stampedChunkHashesCache.Contains(s.ChunkHash))
                .Select(s => new PostageStamp(
                    postageBatchCache.BatchId,
                    s.ChunkHash,
                    s.StampBucketIndex.BucketId,
                    s.StampBucketIndex.BucketCounter))
                .ToArray();
            if (newPostageStamps.Length != 0)
                await dbContext.PostageStamps.CreateAsync(newPostageStamps);
            
            // Update postage batch buckets.
            var updates = new List<UpdateDefinition<PostageBatchCache>>();
            
            var bucketsIncrement = postageBatchCache.Buckets.Zip(newPostageStamper.StampIssuer.Buckets.GetBuckets())
                .Select(pair => pair.Second - pair.First)
                .ToArray();
            for (var i = 0; i < PostageBuckets.BucketsSize; i++)
                if (bucketsIncrement[i] != 0)
                    updates.Add(Builders<PostageBatchCache>.Update.Inc(
                        $"{nameof(PostageBatchCache.Buckets)}.{i}",
                        bucketsIncrement[i]));

            if (updates.Count > 0)
                await dbContext.PostageBatchesCache.FindOneAndUpdateAsync(
                    Builders<PostageBatchCache>.Filter.Eq(m => m.BatchId, postageBatchCache.BatchId),
                    Builders<PostageBatchCache>.Update.Combine(updates),
                    new FindOneAndUpdateOptions<PostageBatchCache>());
        }

        public async Task<PostageBatchCache?> TryGetPostageBatchCacheAsync(
            PostageBatchId batchId,
            bool forceRefreshCache = false)
        {
            // Try load existing cache from db.
            var batchCache = await dbContext.PostageBatchesCache.TryFindOneAsync(b => b.BatchId == batchId);
            
            // Try load status from node.
            if (batchCache == null || forceRefreshCache)
            {
                // Remove cached value from db.
                if (batchCache != null)
                    await dbContext.PostageBatchesCache.DeleteAsync(batchCache);
                
                // Get fresh value.
                var nodeLiveInstance = beeNodeLiveManager.TryGetPostageBatchOwnerNode(batchId);
                if (nodeLiveInstance == null) //postage doesn't exist
                    return null;
                var postageInfo = await nodeLiveInstance.GetPostageBatchAsync(batchId);
                var (bucketsLiveCollisions, depth) = await nodeLiveInstance.GetPostageBatchBucketsCollisionsAsync(batchId);
                
                // Cache new value on db.
                batchCache = new PostageBatchCache(
                    batchId,
                    bucketsLiveCollisions.ToArray(),
                    depth,
                    postageInfo.IsImmutable,
                    nodeLiveInstance.Id);
                await dbContext.PostageBatchesCache.CreateAsync(batchCache);
            }

            return batchCache;
        }

        public async Task<PostageBatch?> TryGetPostageBatchDetailsAsync(PostageBatchId batchId)
        {
            var nodeLiveInstance = beeNodeLiveManager.TryGetPostageBatchOwnerNode(batchId);
            if (nodeLiveInstance == null) //if postage doesn't exist
                return null;
            
            return await nodeLiveInstance.GetPostageBatchAsync(batchId);
        }
    }
}
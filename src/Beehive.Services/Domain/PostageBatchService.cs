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
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.Linq;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
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
        ILogger<PostageBatchService> logger,
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
            BzzValue amount,
            int depth,
            string? label,
            bool immutable,
            ulong? gasLimit,
            XDaiValue? gasPrice)
        {
            // Select node.
            var beeNodeInstance = await beeNodeLiveManager.TrySelectHealthyNodeAsync(
                BeeNodeSelectionMode.RoundRobin,
                "buyPostageBatch",
                node => Task.FromResult(node.IsBatchCreationEnabled));

            if (beeNodeInstance is null)
                throw new InvalidOperationException("No healthy nodes available for batch creation");

            // Buy postage.
            var (batchId, txHash) = await beeNodeInstance.Client.BuyPostageBatchAsync(
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
            logger.PostageBatchBought(batchId, beeNodeInstance.Id, depth, amount, immutable, txHash);
            
            return (batchId, txHash);
        }

        public async Task<EthTxHash> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            ulong? gasLimit,
            XDaiValue? gasPrice)
        {
            // Acquire lock on postage batch.
            await using var batchLockHandler = await AcquireLockAsync(batchId, true);
            
            // Try get cached postage batch.
            var postageCache = await dbContext.PostageBatchesCache.TryFindOneAsync(b => b.BatchId == batchId);
            if (postageCache is null)
                throw new KeyNotFoundException();
            
            // Dilute on node.
            var node = await TryGetPostageBatchOwnerNodeAsync(batchId);
            if (node == null)
                throw new KeyNotFoundException();
            var txHash = await node.DilutePostageBatchAsync(batchId, depth, gasLimit, gasPrice);
            
            // Update cache.
            postageCache.Depth = depth;
            await dbContext.SaveChangesAsync();
            logger.PostageBatchDiluted(batchId, node.Id, depth, txHash);
            
            return txHash;
        }

        public async Task<(PostageBatch PostageBatch, EthAddress Owner)[]> GetGlobalValidPostageBatchesAsync()
        {
            // Select node.
            var beeNodeInstance = await beeNodeLiveManager.TrySelectHealthyNodeAsync(
                BeeNodeSelectionMode.RoundRobin,
                "getGlobalPostageBatches");

            if (beeNodeInstance is null)
                throw new InvalidOperationException("No healthy nodes available for batch read");

            // Get globale batches from node.
            return await beeNodeInstance.Client.GetGlobalValidPostageBatchesAsync();
        }

        public async Task<IEnumerable<PostageBatchResult>> GetOwnedPostageBatchesAsync()
        {
            var nodes = beeNodeLiveManager.AllNodes.ToArray();

            // Ask the alive nodes for their batches, in parallel. A node that is not alive is not asked.
            Dictionary<string, Task<PostageBatchResponse[]>> nodeTasks = []; //node id -> owned batches
            Dictionary<string, string> nodeErrors = []; //node id -> reason the node could not answer
            foreach (var node in nodes)
            {
                var notAliveReason = node.Status.NotAliveReason;
                if (notAliveReason is null)
                    nodeTasks[node.Id] = node.Client.GetOwnedPostageBatchesAsync();
                else
                    nodeErrors[node.Id] = notAliveReason;
            }

            List<PostageBatchResult> postageBatches = [];
            foreach (var (nodeId, task) in nodeTasks)
            {
                try
                {
                    postageBatches.AddRange((await task).Select(b => new PostageBatchResult(b.PostageBatch)));
                }
                catch (Exception e) when (e.IsBeeNodeCallFailure())
                {
                    var reason = e.GetBeeNodeFailureReason();
                    logger.BeeNodeCallFailed(nodeId, "getOwnedPostageBatches", reason, e);
                    nodeErrors[nodeId] = reason;
                }
            }

            // Report the batches of the nodes that could not answer from the cache, each with its node's reason.
            if (nodeErrors.Count > 0)
            {
                var failedNodeIds = nodeErrors.Keys.ToArray();
                var cachedBatches = await dbContext.PostageBatchesCache.QueryElementsAsync(elements =>
                    elements.Where(b => failedNodeIds.Contains(b.OwnerNodeId))
                        .ToListAsync());
                postageBatches.AddRange(cachedBatches.Select(b => new PostageBatchResult(b, nodeErrors[b.OwnerNodeId])));
            }

            return postageBatches;
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
            ArgumentNullException.ThrowIfNull(postageBatchCache);
            ArgumentNullException.ThrowIfNull(newPostageStamper);
            
            // Add new postage stamps.
            var newPostageStamps = newPostageStamper.StampStore.GetItems()
                .Where(s => !stampedChunkHashesCache.Contains(s.ChunkHash))
                .Select(s => new PostageStamp(
                    postageBatchCache.BatchId,
                    s.ChunkHash,
                    s.BucketIndex.BucketId,
                    s.BucketIndex.BucketCounter))
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
                await dbContext.PostageBatchesCache.TryFindOneAndUpdateAsync(
                    Builders<PostageBatchCache>.Filter.Eq(m => m.BatchId, postageBatchCache.BatchId),
                    Builders<PostageBatchCache>.Update.Combine(updates),
                    new FindOneAndUpdateOptions<PostageBatchCache>());
        }

        public async Task<EthTxHash> TopUpPostageBatchAsync(
            PostageBatchId batchId,
            BzzValue amount,
            ulong? gasLimit,
            XDaiValue? gasPrice)
        {
            // Acquire lock on postage batch.
            await using var batchLockHandler = await AcquireLockAsync(batchId, true);

            // Top up on node.
            var node = await TryGetPostageBatchOwnerNodeAsync(batchId);
            if (node == null)
                throw new KeyNotFoundException();
            
            var txHash = await node.TopUpPostageBatchAsync(
                batchId,
                amount,
                gasLimit,
                gasPrice);
            logger.PostageBatchToppedUp(batchId, node.Id, amount, txHash);

            return txHash;
        }
        
        public Task<ResourceLockHandler<ChunkPushLock>?> TryAcquireChunkPushLockAsync(
            PostageBatchId batchId) =>
            resourceLockService.TryAcquireLockAsync(
                () => new ChunkPushLock(batchId),
                dbContext.ChunkPushLocks,
                batchId.ToString(),
                true);

        //*** OLD code to receive postage batch cache from node. See: https://etherna.atlassian.net/browse/BHM-198
        // public async Task<PostageBatchCache?> TryGetPostageBatchCacheAsync(
        //     PostageBatchId batchId,
        //     bool forceRefreshCache = false)
        // {
        //     // Try load existing cache from db.
        //     var batchCache = await dbContext.PostageBatchesCache.TryFindOneAsync(b => b.BatchId == batchId);
        //     
        //     // Try load status from node.
        //     if (batchCache == null || forceRefreshCache)
        //     {
        //         // Remove cached value from db.
        //         if (batchCache != null)
        //             await dbContext.PostageBatchesCache.DeleteAsync(batchCache);
        //         
        //         // Get fresh value.
        //         var nodeLiveInstance = await TryGetPostageBatchOwnerNodeAsync(batchId);
        //         if (nodeLiveInstance == null) //postage doesn't exist
        //             return null;
        //         var postageInfo = await nodeLiveInstance.GetPostageBatchAsync(batchId);
        //         var (bucketsLiveCollisions, depth) = await nodeLiveInstance.GetPostageBatchBucketsCollisionsAsync(batchId);
        //         
        //         // Cache new value on db.
        //         batchCache = new PostageBatchCache(
        //             batchId,
        //             bucketsLiveCollisions.ToArray(),
        //             depth,
        //             postageInfo.IsImmutable,
        //             nodeLiveInstance.Id);
        //         await dbContext.PostageBatchesCache.CreateAsync(batchCache);
        //     }
        //
        //     return batchCache;
        // }
        //***

        public async Task<PostageBatchResult?> TryGetPostageBatchDetailsAsync(PostageBatchId batchId)
        {
            var postageCache = await dbContext.PostageBatchesCache.TryFindOneAsync(b => b.BatchId == batchId);
            if (postageCache is null) //if postage doesn't exist
                return null;
            var nodeLiveInstance = TryGetPostageBatchOwnerNode(postageCache);
            if (nodeLiveInstance is null)
                return null;

            // Don't ask a node that is not alive: the heartbeat already knows why it can't answer.
            var notAliveReason = nodeLiveInstance.Status.NotAliveReason;
            if (notAliveReason is not null)
                return new PostageBatchResult(postageCache, notAliveReason);

            try
            {
                return new PostageBatchResult(await nodeLiveInstance.GetPostageBatchAsync(batchId));
            }
            catch (SwarmSdkApiException e) when (e.StatusCode == 404)
            {
                return null; //the node's answer: the batch is gone
            }
            catch (Exception e) when (e.IsBeeNodeCallFailure())
            {
                //the node not answering: report what the cache knows, with the reason
                var reason = e.GetBeeNodeFailureReason();
                logger.BeeNodeCallFailed(nodeLiveInstance.Id, "getPostageBatch", reason, e);
                return new PostageBatchResult(postageCache, reason);
            }
        }

        public BeeNodeLiveInstance? TryGetPostageBatchOwnerNode(PostageBatchCache postageBatchCache)
        {
            ArgumentNullException.ThrowIfNull(postageBatchCache);

            return beeNodeLiveManager.AllNodes.FirstOrDefault(n => n.Id == postageBatchCache.OwnerNodeId);
        }

        public async Task<BeeNodeLiveInstance?> TryGetPostageBatchOwnerNodeAsync(PostageBatchId batchId)
        {
            var postageCache = await dbContext.PostageBatchesCache.TryFindOneAsync(b => b.BatchId == batchId);
            if (postageCache is null)
                return null;

            return TryGetPostageBatchOwnerNode(postageCache);
        }

        public async Task VerifyPostageBatchUsableAsync(PostageBatchCache postageBatchCache)
        {
            ArgumentNullException.ThrowIfNull(postageBatchCache);

            // Ask the owner node, unless the heartbeat already knows it can't answer.
            var ownerNode = TryGetPostageBatchOwnerNode(postageBatchCache);
            if (ownerNode is null)
                throw new BeeNodeUnavailableException(
                    $"Owner node {postageBatchCache.OwnerNodeId} of the batch is not registered");
            var notAliveReason = ownerNode.Status.NotAliveReason;
            if (notAliveReason is not null)
                throw new BeeNodeUnavailableException(notAliveReason);

            PostageBatch postageBatch;
            try
            {
                postageBatch = await ownerNode.GetPostageBatchAsync(postageBatchCache.BatchId);
            }
            catch (SwarmSdkApiException e) when (e.StatusCode == 404)
            {
                //the node's answer: the batch is gone
                throw new PostageBatchNotFoundException(
                    $"Batch {postageBatchCache.BatchId} not found on its owner node {ownerNode.Id}", e);
            }
            catch (Exception e) when (e.IsBeeNodeCallFailure())
            {
                throw new BeeNodeUnavailableException(e.GetBeeNodeFailureReason(), e);
            }

            if (!postageBatch.IsUsable)
                throw new PostageBatchNotUsableException(
                    $"Batch {postageBatchCache.BatchId} is not usable yet on its owner node {ownerNode.Id}");
        }
    }
}
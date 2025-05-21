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
using Etherna.BeeNet.Hashing.Postage;
using Etherna.BeeNet.Hashing.Signer;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Stores;
using Etherna.MongoDB.Driver.Linq;
using Etherna.MongODM.Core.Serialization.Modifiers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PostageStamp = Etherna.BeeNet.Models.PostageStamp;

namespace Etherna.Beehive.Services.Domain
{
    public sealed class DataService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        IPostageBatchService postageBatchService,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IDataService
    {
        public async Task<SwarmChunkReference> UploadAsync(
            PostageBatchId batchId,
            EthAddress? batchOwner,
            bool useChunkCompaction,
            bool pinContent,
            Func<IChunkStore, IPostageStamper, Task<SwarmChunkReference>> chunkingFuncAsync,
            IDictionary<SwarmHash, PostageStamp>? presignedPostageStamps = null)
        {
            ArgumentNullException.ThrowIfNull(chunkingFuncAsync, nameof(chunkingFuncAsync));
            
            // Acquire lock on postage batch.
            await using var batchLockHandler = await postageBatchService.AcquireLockAsync(batchId, useChunkCompaction);
            
            // Verify postage batch, load status and build postage stamper.
            var postageBatchCache = await dbContext.PostageBatchesCache.TryFindOneAsync(b => b.BatchId == batchId);
            if (postageBatchCache is null)
                throw new KeyNotFoundException();
            var postageStampsCache = await dbContext.PostageStamps.QueryElementsAsync(
                elements => elements.Where(s => s.BatchId == batchId)
                    .ToListAsync());

            using var postageBuckets = new PostageBuckets(postageBatchCache.Buckets.ToArray());
            var stampStore = new MemoryStampStore(
                postageStampsCache.Select(stamp =>
                    new StampStoreItem(
                        batchId,
                        stamp.ChunkHash,
                        new PostageBucketIndex(
                            stamp.BucketId,
                            stamp.BucketCounter))));
            var postageStamper = new PostageStamper(
                new FakeSigner(),
                new PostageStampIssuer(
                    new PostageBatch(
                        id: postageBatchCache.BatchId,
                        amount: 0,
                        blockNumber: 0,
                        depth: postageBatchCache.Depth,
                        exists: true,
                        isImmutable: postageBatchCache.IsImmutable,
                        isUsable: true,
                        label: null,
                        ttl: TimeSpan.FromDays(3650),
                        utilization: 0),
                    batchOwner,
                    postageBuckets),
                stampStore,
                presignedPostageStamps);

            // Create pin if required.
            ChunkPin? pin = null;
            if (pinContent)
            {
                pin = new ChunkPin(null); //set root hash later
                await dbContext.ChunkPins.CreateAsync(pin);
                pin = await dbContext.ChunkPins.FindOneAsync(pin.Id);
            }

            // Upload.
            ConcurrentBag<UploadedChunkRef> chunkRefs = [];
            SwarmChunkReference hashingResult;
            await using (
                var dbChunkStore = new BeehiveChunkStore(
                    beeNodeLiveManager,
                    dbContext,
                    serializerModifierAccessor,
                    onSavingChunk: c =>
                    {
                        if (pin != null)
                            c.AddPin(pin);
                        chunkRefs.Add(new(c.Hash, batchId));
                    }))
            {
                //create and store chunks
                hashingResult = await chunkingFuncAsync(dbChunkStore, postageStamper);

                await dbChunkStore.FlushSaveAsync();
            }
            
            // Add new stamped chunks to postage batch cache.
            await postageBatchService.StoreStampedChunksAsync(
                postageBatchCache,
                postageStampsCache.Select(s => s.ChunkHash).ToHashSet(),
                postageStamper);

            // Confirm chunk's push.
            await dbContext.ChunkPushQueue.CreateAsync(chunkRefs);

            // Update pin, if required.
            if (pin != null)
            {
                pin.SucceededProvisional(hashingResult, chunkRefs.Count);
                await dbContext.SaveChangesAsync();
            }

            return hashingResult;
        }
    }
}
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

using Etherna.Beehive.Areas.Api.Bee.DtoModels;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Hashing.Pipeline;
using Etherna.BeeNet.Hashing.Postage;
using Etherna.BeeNet.Hashing.Signer;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class BytesControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        IPostageBatchService postageBatchService)
        : IBytesControllerService
    {
        // Methods.
        public async Task<IActionResult> DownloadBytesAsync(
            SwarmHash hash,
            XorEncryptKey? encryptionKey,
            bool recursiveEncryption)
        {
            using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext);
            var chunkJoiner = new ChunkJoiner(chunkStore);
            var dataStream = await chunkJoiner.GetJoinedChunkDataAsync(new SwarmChunkReference(
                hash,
                encryptionKey,
                recursiveEncryption));

            return new FileStreamResult(dataStream, "application/octet-stream");
        }

        public async Task<IActionResult> UploadBytesAsync(
            PostageBatchId batchId,
            ushort compactLevel,
            bool pinContent,
            HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
            
            // Verify and load postage batch status.
            var postageBucketsStatus = await postageBatchService.TryGetPostageBucketsAsync(batchId);
            if (postageBucketsStatus is null)
                throw new KeyNotFoundException();
            using var postageBuckets = new PostageBuckets(postageBucketsStatus.BucketsCollisions.ToArray());

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
            var dbChunkStore = new BeehiveChunkStore(
                beeNodeLiveManager,
                dbContext,
                onSavingChunk: c =>
                {
                    if (pin != null)
                        c.AddPin(pin);
                    chunkRefs.Add(new(c.Hash, batchId));
                });
            await using (dbChunkStore.ConfigureAwait(false))
            {
                // Create and store chunks.
                using var fileHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                    dbChunkStore,
                    new PostageStamper(
                        new FakeSigner(),
                        new PostageStampIssuer(
                            PostageBatch.MaxDepthInstance,
                            postageBuckets),
                        new MemoryStampStore()),
                    RedundancyLevel.None,
                    false,
                    compactLevel,
                    null);
                hashingResult = await fileHasherPipeline.HashDataAsync(httpContext.Request.Body).ConfigureAwait(false);

                await dbChunkStore.FlushSaveAsync();
            }
            
            // Increment cached postage buckets.
            await postageBatchService.IncrementPostageBucketsCacheAsync(
                postageBucketsStatus,
                postageBuckets);

            // Confirm chunk's push.
            await dbContext.ChunkPushQueue.CreateAsync(chunkRefs);

            // Update pin, if required.
            if (pin != null)
            {
                pin.SucceededProvisional(hashingResult);
                await dbContext.SaveChangesAsync();
            }

            return new JsonResult(new ChunkReferenceDto(
                hashingResult.Hash,
                hashingResult.EncryptionKey,
                hashingResult.UseRecursiveEncryption))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }
    }
}
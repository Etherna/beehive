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
using Etherna.Beehive.Services.Chunks;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Hashing.Pipeline;
using Etherna.BeeNet.Hashing.Postage;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class BytesControllerService(
        IBeehiveChunkStore beehiveChunkStore,
        IBeehiveDbContext dbContext)
        : IBytesControllerService
    {
        public async Task<IActionResult> DownloadBytesAsync(
            SwarmHash hash)
        {
            var chunkJoiner = new ChunkJoiner(beehiveChunkStore);
            var dataStream = await chunkJoiner.GetJoinedChunkDataAsync(new SwarmChunkReference(hash, null, false));

            return new FileStreamResult(dataStream, "application/octet-stream");
        }

        public async Task<IActionResult> UploadBytesAsync(
            PostageBatchId batchId,
            ushort compactLevel,
            bool pinContent,
            HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
            
            // Create db chunk store, with pinning if required.
            ChunkPin? pin = null;
            List<UploadedChunkRef> chunkRefs = [];
            if (pinContent)
            {
                pin = new ChunkPin(null); //set root hash later
                await dbContext.ChunkPins.CreateAsync(pin);
                pin = await dbContext.ChunkPins.FindOneAsync(pin.Id);
            }
            var dbChunkStore = new DbChunkStore(
                dbContext,
                onSavingChunk: c =>
                {
                    if (pin != null)
                        c.AddPin(pin);
                    chunkRefs.Add(new(c.Hash, batchId));
                });
            
            // Create and store chunks.
            using var fileHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                dbChunkStore,
                new FakePostageStamper(),
                RedundancyLevel.None,
                false,
                compactLevel,
                null);
            var hashingResult = await fileHasherPipeline.HashDataAsync(httpContext.Request.Body).ConfigureAwait(false);
            await dbContext.ChunkPushQueue.CreateAsync(chunkRefs);
            
            // Update pin, if required.
            if (pin != null)
            {
                pin.UpgradeProvisional(hashingResult);
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
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
using Etherna.Beehive.Configs;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Hashing.Pipeline;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class BytesControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IDataService dataService,
        IBeehiveDbContext dbContext)
        : IBytesControllerService
    {
        // Methods.
        public async Task<IActionResult> DownloadBytesAsync(
            SwarmHash hash,
            XorEncryptKey? encryptionKey,
            bool recursiveEncryption)
        {
            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext);
            var chunkJoiner = new ChunkJoiner(chunkStore);
            var dataStream = await chunkJoiner.GetJoinedChunkDataAsync(new SwarmChunkReference(
                hash,
                encryptionKey,
                recursiveEncryption));

            return new FileStreamResult(dataStream, BeehiveHttpConsts.OctetStreamContentType);
        }

        public async Task<IActionResult> UploadBytesAsync(
            PostageBatchId batchId,
            ushort compactLevel,
            bool pinContent,
            HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
            
            var hashingResult = await dataService.UploadAsync(
                batchId,
                null,
                compactLevel > 0,
                pinContent,
                async (chunkStore, postageStamper) =>
                {
                    using var fileHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                        chunkStore,
                        postageStamper,
                        RedundancyLevel.None,
                        false,
                        compactLevel,
                        null);
                    return await fileHasherPipeline.HashDataAsync(httpContext.Request.Body);
                });

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
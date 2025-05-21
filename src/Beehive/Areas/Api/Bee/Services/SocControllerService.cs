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
using Etherna.BeeNet.Models;
using Etherna.MongODM.Core.Serialization.Modifiers;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class SocControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IDataService dataService,
        IBeehiveDbContext dbContext,
        ISerializerModifierAccessor serializerModifierAccessor)
        : ISocControllerService
    {
        public async Task<IActionResult> ResolveSocAsync(
            EthAddress owner,
            SwarmSocIdentifier identifier,
            bool onlyRootChunk,
            HttpResponse response)
        {
            ArgumentNullException.ThrowIfNull(response, nameof(response));
            
            // Try find soc.
            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext, serializerModifierAccessor);
            var chunk = await chunkStore.TryGetAsync(
                SwarmSoc.BuildHash(identifier, owner, new Hasher())).ConfigureAwait(false);

            if (chunk is not SwarmSoc soc)
                throw new InvalidOperationException("Chunk is not a single owner chunk");
            
            // Build response headers.
            response.Headers.Append(SwarmHttpConsts.SwarmSocSignatureHeader, soc.Signature.ToString());
            response.Headers.Append(CorsConstants.AccessControlExposeHeaders, SwarmHttpConsts.SwarmSocSignatureHeader);
            
            // Return content.
            //if only root, returns chunk's data
            if (onlyRootChunk)
                return new FileContentResult(
                    soc.InnerChunk.Data.ToArray(),
                    BeehiveHttpConsts.ApplicationOctetStreamContentType);

            //else return joined data
            var dataStream = ChunkDataStream.BuildNew(soc.InnerChunk, null, false, chunkStore);
            return new FileStreamResult(
                dataStream,
                BeehiveHttpConsts.ApplicationOctetStreamContentType);
        }

        public async Task<IActionResult> UploadSocAsync(
            EthAddress owner,
            SwarmSocIdentifier identifier,
            SwarmSocSignature signature,
            PostageBatchId? batchId,
            PostageStamp? postageStamp,
            Stream dataStream,
            bool pinContent)
        {
            ArgumentNullException.ThrowIfNull(dataStream, nameof(dataStream));
            
            if (!batchId.HasValue && postageStamp == null)
                throw new ArgumentNullException(nameof(batchId), "Batch id or postage stamp are required");
            if (batchId.HasValue && postageStamp != null && batchId.Value != postageStamp.BatchId)
                throw new ArgumentException("Postage batch Id doesn't match with postage stamp's batch Id");
            
            // Read data.
            byte[] data;
            using (var dataMemoryStream = new MemoryStream())
            {
                await dataStream.CopyToAsync(dataMemoryStream);
                data = dataMemoryStream.ToArray();
            }
            
            if (data.Length < SwarmCac.SpanSize)
                throw new ArgumentOutOfRangeException(nameof(dataStream), data.Length, $"Data is smaller than {SwarmCac.SpanSize} bytes");
            if (data.Length > SwarmCac.SpanDataSize)
                throw new ArgumentOutOfRangeException(nameof(dataStream), data.Length, $"Data exceeds max size of {SwarmCac.SpanDataSize} bytes");
            
            // Build SOC chunk.
            var hasher = new Hasher();
            var chunkBmt = new SwarmChunkBmt(hasher);
            var soc = new SwarmSoc(
                identifier,
                owner,
                new SwarmCac(chunkBmt.Hash(data), data),
                null,
                signature);
            soc.BuildHash(hasher);
            
            // Validate new SOC chunk.
            if (!soc.ValidateSoc(hasher))
                throw new ArgumentException("Invalid chunk");

            // Stamp and store chunk.
            var chunkReference = await dataService.UploadAsync(
                batchId ?? postageStamp?.BatchId ?? throw new InvalidOperationException(),
                owner,
                false,
                pinContent,
                async (chunkStore, postageStamper) =>
                {
                    postageStamper.Stamp(soc.Hash);
                    await chunkStore.AddAsync(soc).ConfigureAwait(false);

                    return new SwarmChunkReference(soc.Hash, null, false);
                },
                postageStamp is null
                    ? null
                    : new Dictionary<SwarmHash, PostageStamp>
                    {
                        [soc.Hash] = postageStamp
                    });

            return new JsonResult(new SimpleChunkReferenceDto(chunkReference.Hash))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }
    }
}
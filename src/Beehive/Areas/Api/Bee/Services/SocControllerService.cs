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
using Etherna.Beehive.Services.Domain;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Mvc;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class SocControllerService(
        IDataService dataService)
        : ISocControllerService
    {
        public async Task<IActionResult> UploadSocAsync(
            EthAddress owner,
            string id,
            string signature,
            PostageBatchId? batchId,
            PostageStamp? postageStamp,
            Stream dataStream)
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
            
            if (data.Length < SwarmChunk.SpanSize)
                throw new ArgumentOutOfRangeException(nameof(dataStream), data.Length, $"Data is smaller than {SwarmChunk.SpanSize} bytes");
            if (data.Length > SwarmChunk.SpanAndDataSize)
                throw new ArgumentOutOfRangeException(nameof(dataStream), data.Length, $"Data exceeds max size of {SwarmChunk.SpanAndDataSize} bytes");
            
            // Build SOC chunk.
            var soc = new SingleOwnerChunk(
                id.HexToByteArray(),
                signature.HexToByteArray(),
                owner,
                data);

            var hasher = new Hasher();
            var socChunk = new SwarmChunk(
                soc.BuildHash(hasher),
                soc.ToByteArray());
            
            // Validate new SOC chunk.
            if (!SingleOwnerChunk.IsValidChunk(socChunk, hasher))
                throw new ArgumentException("Invalid chunk");

            // Stamp and store chunk.
            var chunkReference = await dataService.UploadAsync(
                batchId ?? postageStamp?.BatchId ?? throw new InvalidOperationException(),
                owner,
                false,
                false,
                async (chunkStore, postageStamper) =>
                {
                    postageStamper.Stamp(socChunk.Hash);
                    await chunkStore.AddAsync(socChunk).ConfigureAwait(false);

                    return new SwarmChunkReference(socChunk.Hash, null, false);
                },
                postageStamp is null
                    ? null
                    : new Dictionary<SwarmHash, PostageStamp>
                    {
                        [socChunk.Hash] = postageStamp
                    });

            return new JsonResult(new SimpleChunkReferenceDto(chunkReference.Hash));
        }
    }
}
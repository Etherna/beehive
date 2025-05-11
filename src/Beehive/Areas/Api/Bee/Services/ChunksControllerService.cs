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
using Etherna.BeeNet.Models;
using Etherna.MongODM.Core.Serialization.Modifiers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PostageStamp = Etherna.BeeNet.Models.PostageStamp;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class ChunksControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IDataService dataService,
        IBeehiveDbContext dbContext,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IChunksControllerService
    {
        [SuppressMessage("ReSharper", "EmptyGeneralCatchClause")]
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public async Task<IActionResult> BulkUploadChunksAsync(
            Stream dataStream,
            PostageBatchId batchId)
        {
            ArgumentNullException.ThrowIfNull(dataStream, nameof(dataStream));
            
            // Read payload.
            byte[] payload;
            await using (var memoryStream = new MemoryStream())
            {
                await dataStream.CopyToAsync(memoryStream);
                payload = memoryStream.ToArray();
            }
            
            // Try to consume data from request.
            var chunkBmt = new SwarmChunkBmt();
            List<SwarmCac> chunks = [];
            for (int i = 0; i < payload.Length;)
            {
                //read chunk size
                var chunkSize = ReadUshort(payload.AsSpan()[i..(i + sizeof(ushort))]);
                i += sizeof(ushort);
                if (chunkSize > SwarmCac.SpanDataSize)
                    throw new InvalidOperationException("Invalid chunk size");

                //read and hash chunk payload
                var chunkPayload = payload[i..(i + chunkSize)];
                i += chunkSize;
                
                var hash = chunkBmt.Hash(chunkPayload);
                chunkBmt.Clear();
                var chunk = new SwarmCac(hash, chunkPayload);
                chunks.Add(chunk);
                
                //verify hash
                var checkHash = ReadSwarmHash(payload.AsSpan()[i..(i + SwarmHash.HashSize)]);
                i += SwarmHash.HashSize;
                if (checkHash != hash)
                    throw new InvalidDataException("Invalid hash with provided data");
            }
            
            // Store chunk.
            await dataService.UploadAsync(
                batchId,
                null,
                false,
                false,
                async (chunkStore, postageStamper) =>
                {
                    foreach (var chunk in chunks)
                    {
                        postageStamper.Stamp(chunk.Hash);
                        await chunkStore.AddAsync(chunk);
                    }

                    return new SwarmChunkReference(SwarmHash.Zero, null, false);
                });

            // Reply.
            return new CreatedResult();
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        [SuppressMessage("ReSharper", "EmptyGeneralCatchClause")]
        public async Task<IActionResult> DownloadChunkAsync(SwarmHash hash)
        {
            await using var chunkStore = new BeehiveChunkStore(
                beeNodeLiveManager,
                dbContext,
                serializerModifierAccessor);
            
            var chunk = await chunkStore.GetAsync(hash);

            return new FileContentResult(
                chunk.GetFullPayload().ToArray(),
                BeehiveHttpConsts.BinaryOctetStreamContentType);
        }

        public async Task<IActionResult> GetChunkHeadersAsync(
            SwarmHash hash)
        {
            await using var chunkStore = new BeehiveChunkStore(
                beeNodeLiveManager,
                dbContext,
                serializerModifierAccessor);

            var hasChunk = await chunkStore.HasChunkAsync(hash);
            return hasChunk ? new OkResult() : new NotFoundResult();
        }

        public async Task<IActionResult> UploadChunkAsync(
            Stream dataStream,
            PostageBatchId? batchId,
            PostageStamp? postageStamp)
        {
            ArgumentNullException.ThrowIfNull(dataStream, nameof(dataStream));
            
            // Read payload.
            byte[] payload;
            await using (var memoryStream = new MemoryStream())
            {
                await dataStream.CopyToAsync(memoryStream);
                payload = memoryStream.ToArray();
            }
            
            // Hash Content Addressed Chunk.
            var chunkBmt = new SwarmChunkBmt();
            var hash = chunkBmt.Hash(
                payload[..SwarmCac.SpanSize].ToArray(),
                payload[SwarmCac.SpanSize..].ToArray());
            var chunk = new SwarmCac(hash, payload);
            
            // Recover batch owner, if required.
            EthAddress? owner = null;
            if (postageStamp != null)
                owner = postageStamp.RecoverBatchOwner(hash, chunkBmt.Hasher);
            
            // Store chunk.
            var hashingResult = await dataService.UploadAsync(
                batchId ?? postageStamp?.BatchId ?? throw new InvalidOperationException(),
                owner,
                false,
                false,
                async (chunkStore, postageStamper) =>
                {
                    postageStamper.Stamp(hash);
                    await chunkStore.AddAsync(chunk);

                    return new SwarmChunkReference(hash, null, false);
                },
                postageStamp is null
                    ? null
                    : new Dictionary<SwarmHash, PostageStamp>
                    {
                        [hash] = postageStamp
                    });
            
            return new JsonResult(new SimpleChunkReferenceDto(hashingResult.Hash))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }

        // Helpers.
        private static SwarmHash ReadSwarmHash(Span<byte> payload)
        {
            if (payload.Length != SwarmHash.HashSize)
                throw new ArgumentOutOfRangeException(nameof(payload));
            
            var valueByteArray = new byte[SwarmHash.HashSize];
            for (int i = 0; i < valueByteArray.Length; i++)
                valueByteArray[i] = payload[i];
            return SwarmHash.FromByteArray(valueByteArray);
        }
        
        private static ushort ReadUshort(Span<byte> payload)
        {
            if (payload.Length != sizeof(ushort))
                throw new ArgumentOutOfRangeException(nameof(payload));
            
            var valueByteArray = new byte[sizeof(ushort)];
            for (int i = 0; i < valueByteArray.Length; i++)
                valueByteArray[i] = payload[i];
            return BitConverter.ToUInt16(valueByteArray);
        }
    }
}
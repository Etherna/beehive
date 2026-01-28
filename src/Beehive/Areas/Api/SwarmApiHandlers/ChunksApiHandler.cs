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

using Etherna.Beehive.Areas.Api.DtoModels;
using Etherna.Beehive.Configs;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Models;
using Etherna.MongODM.Core.Serialization.Modifiers;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public sealed class ChunksApiHandler(
        IBeeNodeLiveManager beeNodeLiveManager,
        IDataService dataService,
        IBeehiveDbContext dbContext,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IChunksApiHandler
    {
        public Task<IResult> DownloadChunkAsync(SwarmHash hash) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                await using var chunkStore = new BeehiveChunkStore(
                    beeNodeLiveManager,
                    dbContext,
                    serializerModifierAccessor);

                var chunk = await chunkStore.GetAsync(hash);

                return Results.File(
                    chunk.GetFullPayload().ToArray(),
                    BeehiveHttpConsts.BinaryOctetStreamContentType);
            });

        public Task<IResult> GetChunkHeadersAsync(SwarmHash hash) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                await using var chunkStore = new BeehiveChunkStore(
                    beeNodeLiveManager,
                    dbContext,
                    serializerModifierAccessor);

                var hasChunk = await chunkStore.HasChunkAsync(hash);
                return hasChunk ? Results.Ok() : Results.NotFound();
            });

        public Task<IResult> BulkUploadChunksAsync(Stream dataStream, PostageBatchId batchId) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                ArgumentNullException.ThrowIfNull(dataStream);
                
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

                        return new SwarmReference(SwarmHash.Zero, null);
                    });

                // Reply.
                return Results.Created();
            });

        public Task<IResult> UploadChunkAsync(Stream dataStream, PostageBatchId? batchId, PostageStamp? postageStamp) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                ArgumentNullException.ThrowIfNull(dataStream);
            
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
                    owner = postageStamp.Value.RecoverBatchOwner(hash, chunkBmt.Hasher);
            
                // Store chunk.
                var reference = await dataService.UploadAsync(
                    batchId ?? postageStamp?.BatchId ?? throw new InvalidOperationException(),
                    owner,
                    false,
                    false,
                    async (chunkStore, postageStamper) =>
                    {
                        postageStamper.Stamp(hash);
                        await chunkStore.AddAsync(chunk);

                        return new SwarmReference(hash, null);
                    },
                    postageStamp is null
                        ? null
                        : new Dictionary<SwarmHash, PostageStamp>
                        {
                            [hash] = postageStamp.Value
                        });
            
                return Results.Json(
                    new ChunkReferenceDto(reference),
                    statusCode: StatusCodes.Status201Created);
            });
        
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
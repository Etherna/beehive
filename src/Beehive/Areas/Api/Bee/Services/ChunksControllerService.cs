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
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Extensions;
using Etherna.Beehive.HttpTransformers;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;
using PostageStamp = Etherna.BeeNet.Models.PostageStamp;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class ChunksControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        IHttpForwarder forwarder,
        IHttpContextAccessor httpContextAccessor)
        : IChunksControllerService
    {
        [SuppressMessage("ReSharper", "EmptyGeneralCatchClause")]
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public async Task BulkUploadChunksAsync(
            PostageBatchId batchId,
            HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
            
            // Read payload.
            await using var memoryStream = new MemoryStream();
            await httpContext.Request.Body.CopyToAsync(memoryStream);
            var payload = memoryStream.ToArray();
            
            // Try consume data from request.
            try
            {
                var hasher = new Hasher();
                List<Chunk> chunks = [];
                List<UploadedChunkRef> chunkRefs = [];
                for (int i = 0; i < payload.Length;)
                {
                    //read chunk size
                    var chunkSize = ReadUshort(payload.AsSpan()[i..(i + sizeof(ushort))]);
                    i += sizeof(ushort);
                    if (chunkSize > SwarmCac.SpanDataSize)
                        throw new InvalidOperationException();

                    //read and store chunk payload
                    var chunkPayload = payload[i..(i + chunkSize)];
                    i += chunkSize;
                    var chunkBmt = new SwarmChunkBmt(hasher);
                    var hash = chunkBmt.Hash(
                        chunkPayload[..SwarmCac.SpanSize].ToArray(),
                        chunkPayload[SwarmCac.SpanSize..].ToArray());
                    var chunkRef = new UploadedChunkRef(hash, batchId);

                    //read check hash
                    var checkHash = ReadSwarmHash(payload.AsSpan()[i..(i + SwarmHash.HashSize)]);
                    i += SwarmHash.HashSize;
                    if (checkHash != hash)
                        throw new InvalidDataException("Invalid hash with provided data");

                    chunks.Add(new Chunk(hash, chunkPayload, false));
                    chunkRefs.Add(chunkRef);
                }
                
                // Push data to db.
                await dbContext.Chunks.CreateAsync(chunks);
                await dbContext.ChunkPushQueue.CreateAsync(chunkRefs);

                // Reply.
                httpContext.Response.StatusCode =  StatusCodes.Status201Created;
            }
            catch(InvalidDataException)
            {
                httpContext.Response.StatusCode =  StatusCodes.Status400BadRequest;
            }
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        [SuppressMessage("ReSharper", "EmptyGeneralCatchClause")]
        public async Task<IResult> DownloadChunkAsync(SwarmHash hash)
        {
            // Try to get from chunk's db.
            using var chunkStore = new BeehiveChunkStore(
                beeNodeLiveManager,
                dbContext);
            try
            {
                var chunk = await chunkStore.GetAsync(hash, null);

                return Results.File(
                    chunk.GetFullPayloadToByteArray(),
                    BeehiveHttpConsts.OctetStreamContentType,
                    hash.ToString());
            }
            catch
            {
            } //proceed with forward on any error

            // Select node and forward request.
            var node = await beeNodeLiveManager.SelectDownloadNodeAsync(hash);
            return await node.ForwardRequestAsync(
                forwarder,
                httpContextAccessor.HttpContext!,
                new DownloadHttpTransformer());
        }

        public async Task<IActionResult> UploadChunkAsync(
            PostageBatchId? batchId,
            PostageStamp? postageStamp,
            HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
            
            // Read payload.
            byte[] payload;
            await using (var memoryStream = new MemoryStream())
            {
                await httpContext.Request.Body.CopyToAsync(memoryStream);
                payload = memoryStream.ToArray();
            }
            
            // Try consume data from request.
            try
            {
                var hasher = new Hasher();
                
                //read and store chunk payload
                var chunkBmt = new SwarmChunkBmt(hasher);
                var hash = chunkBmt.Hash(
                    payload[..SwarmCac.SpanSize].ToArray(),
                    payload[SwarmCac.SpanSize..].ToArray());
                var chunkRef = new UploadedChunkRef(hash, batchId!.Value);

                var chunk = new Chunk(hash, payload, false);
                
                // Push data to db.
                await dbContext.Chunks.CreateAsync(chunk);
                await dbContext.ChunkPushQueue.CreateAsync(chunkRef);

                // Reply.
                return new JsonResult(new ChunkReferenceDto(hash, null, false))
                {
                    StatusCode = StatusCodes.Status201Created
                };
            }
            catch(InvalidDataException)
            {
                return new BadRequestResult();
            }
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
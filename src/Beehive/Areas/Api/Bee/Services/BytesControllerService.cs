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
using Etherna.Beehive.Areas.Api.Bee.Results;
using Etherna.Beehive.Configs;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Hashing.Pipeline;
using Etherna.BeeNet.Models;
using Etherna.MongODM.Core.Serialization.Modifiers;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class BytesControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IDataService dataService,
        IBeehiveDbContext dbContext,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IBytesControllerService
    {
        // Methods.
        public async Task<IActionResult> DownloadBytesAsync(SwarmReference reference)
        {
            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext, serializerModifierAccessor);
            var dataStream = await ChunkDataStream.BuildNewAsync(reference, chunkStore);

            return new FileStreamResult(dataStream, BeehiveHttpConsts.ApplicationOctetStreamContentType);
        }

        public async Task<IActionResult> GetBytesHeadersAsync(
            SwarmReference reference,
            HttpResponse response)
        {
            ArgumentNullException.ThrowIfNull(response, nameof(response));
            
            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext, serializerModifierAccessor);
            var chunk = await chunkStore.GetAsync(reference.Hash);
            if (chunk is not SwarmCac cac) //bytes can only read from cac
                return new BeeBadRequestResult();

            ulong dataLength;
            if (reference.IsEncrypted)
            {
                ChunkEncrypter.DecryptChunk(
                    cac,
                    reference.EncryptionKey!.Value,
                    new Hasher(),
                    out var decryptedSpanData);
                dataLength = SwarmCac.SpanToLength(decryptedSpanData[..SwarmCac.SpanSize].Span);
            }
            else
                dataLength = SwarmCac.SpanToLength(cac.Span.Span);

            response.Headers.Append(
                CorsConstants.AccessControlExposeHeaders, new StringValues(
                [
                    HeaderNames.AcceptRanges,
                    HeaderNames.ContentEncoding
                ]));
            response.ContentLength = (long)dataLength;
            response.ContentType = BeehiveHttpConsts.ApplicationOctetStreamContentType;

            return new OkResult();
        }

        public async Task<IActionResult> UploadBytesAsync(
            Stream dataStream,
            PostageBatchId batchId,
            ushort compactLevel,
            bool encrypt,
            bool pinContent)
        {
            var reference = await dataService.UploadAsync(
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
                        encrypt,
                        compactLevel,
                        null);
                    return await fileHasherPipeline.HashDataAsync(dataStream);
                });

            return new JsonResult(new ChunkReferenceDto(reference))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }
    }
}
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
using Etherna.MongODM.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Chunks;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public sealed class BytesApiHandler(
        IBeeNodeLiveManager beeNodeLiveManager,
        IDataService dataService,
        IBeehiveDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IBytesApiHandler
    {
        public Task<IResult> DownloadBytesAsync(
            SwarmReference reference,
            RedundancyLevel redundancyLevel,
            RedundancyStrategy redundancyStrategy,
            bool redundancyStrategyFallback) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                await using var chunkStore =
                    new BeehiveChunkStore(beeNodeLiveManager, dbContext, serializerModifierAccessor);
                var dataStream = await ChunkDataStream.BuildNewAsync(
                    reference,
                    chunkStore,
                    redundancyLevel,
                    redundancyStrategy,
                    redundancyStrategyFallback);

                return Results.Stream(dataStream, BeehiveHttpConsts.ApplicationOctetStreamContentType);
            });

        public Task<IResult> GetBytesHeadersAsync(SwarmReference reference) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                await using var chunkStore =
                    new BeehiveChunkStore(beeNodeLiveManager, dbContext, serializerModifierAccessor);
                var chunk = await chunkStore.GetAsync(reference.Hash);
                if (chunk is not SwarmCac cac) //bytes can only read from cac
                    throw new SwarmChunkTypeException(chunk);

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

                var response = httpContextAccessor.HttpContext!.Response;
                response.Headers.Append(
                    CorsConstants.AccessControlExposeHeaders, new StringValues(
                    [
                        HeaderNames.AcceptRanges,
                        HeaderNames.ContentEncoding
                    ]));
                response.ContentLength = (long)dataLength;
                response.ContentType = BeehiveHttpConsts.ApplicationOctetStreamContentType;

                return Results.Ok();
            });

        public Task<IResult> UploadBytesAsync(
            Stream dataStream,
            PostageBatchId batchId,
            ushort compactLevel,
            bool encrypt,
            bool pinContent,
            RedundancyLevel redundancyLevel) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var reference = await dataService.UploadAsync(
                    batchId: batchId,
                    batchOwner: null,
                    useChunkCompaction: compactLevel > 0,
                    pinContent: pinContent,
                    chunkingFuncAsync: async (chunkStore, postageStamper) =>
                    {
                        using var fileHasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                            chunkStore,
                            postageStamper,
                            redundancyLevel,
                            encrypt,
                            compactLevel,
                            null);
                        return await fileHasherPipeline.HashDataAsync(dataStream);
                    });

                return Results.Json(
                    new ChunkReferenceDto(reference),
                    CommonConsts.SwarmJsonSerializerOptions,
                    statusCode: StatusCodes.Status201Created);
            });
    }
}
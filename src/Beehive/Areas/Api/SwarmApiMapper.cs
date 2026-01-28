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
using Etherna.Beehive.Areas.Api.SwarmApiHandlers;
using Etherna.Beehive.Configs;
using Etherna.Beehive.Extensions;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace Etherna.Beehive.Areas.Api
{
    public static class SwarmApiMapper
    {
        // Methods.
        public static void MapSwarmApi(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);
            
            // APIs.
            ConfigureMaps(app.MapGroup("").WithMetadata(new SwarmApiMarker()), false);
            ConfigureMaps(app.MapGroup("/v1").WithMetadata(new SwarmV1ApiMarker()), true);
        }
        
        // Helpers.
        private static void ConfigureMaps(RouteGroupBuilder builder, bool hasBeePrefix)
        {
            //batches
            builder.MapGet("/batches",
                (IBatchesApiHandler handler) => handler.GetGlobalValidPostageBatchesAsync())
                .Produces<GlobalPostageBatchesDto>();
            
            //bytes
            builder.MapGet("/bytes/{reference}",
                    (IBytesApiHandler handler,
                            [FromRoute] SwarmReference reference,
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyLevelHeader)] RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyStrategyHeader)] RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data, 
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyFallbackModeHeader)] bool redundancyStrategyFallback = true) =>
                        handler.DownloadBytesAsync(reference, redundancyLevel, redundancyStrategy, redundancyStrategyFallback))
                .Produces<Stream>(StatusCodes.Status200OK, BeehiveHttpConsts.ApplicationOctetStreamContentType)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapHead("/bytes/{reference}",
                    (IBytesApiHandler handler,
                            [FromRoute] SwarmReference reference) =>
                        handler.GetBytesHeadersAsync(reference))
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapPost("/bytes",
                    (IBytesApiHandler handler,
                            [FromBody] Stream dataStream,
                            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader)] PostageBatchId batchId,
                            [FromHeader(Name = BeehiveHttpConsts.SwarmCompactLevelHeader)] ushort compactLevel = 0,
                            [FromHeader(Name = SwarmHttpConsts.SwarmEncryptHeader)] bool encrypt = false,
                            [FromHeader(Name = SwarmHttpConsts.SwarmPinningHeader)] bool pinContent = false,
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyLevelHeader)] RedundancyLevel redundancyLevel = RedundancyLevel.None) =>
                        handler.UploadBytesAsync(dataStream, batchId, compactLevel, encrypt, pinContent, redundancyLevel))
                .Accepts<Stream>(BeehiveHttpConsts.ApplicationOctetStreamContentType)
                .NotProduces200()
                .Produces<ChunkReferenceDto>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status402PaymentRequired)
                .Produces(StatusCodes.Status404NotFound);
            
            //bzz
            builder.MapGet("/bzz/{**address}",
                    (IBzzApiHandler handler,
                            [FromRoute] string address, //receive address as a raw string: we need to redirect in case it is only an <hash> without final '/'
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyLevelHeader)] RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyStrategyHeader)] RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data, 
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyFallbackModeHeader)] bool redundancyStrategyFallback = true) =>
                        handler.DownloadBzzAsync(
                            address,
                            redundancyLevel,
                            redundancyStrategy,
                            redundancyStrategyFallback))
                .Produces<Stream>()
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapHead("/bzz/{**address}",
                    (IBzzApiHandler handler,
                            [FromRoute] string address,
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyLevelHeader)] RedundancyLevel redundancyLevel = RedundancyLevel.Paranoid,
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyStrategyHeader)] RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data, 
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyFallbackModeHeader)] bool redundancyStrategyFallback = true) =>
                        handler.GetBzzHeadersAsync(
                            address,
                            redundancyLevel,
                            redundancyStrategy,
                            redundancyStrategyFallback))
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapPost("/bzz",
                (IBzzApiHandler handler,
                        [FromQuery] string? name,
                        [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader)] PostageBatchId batchId,
                        [FromHeader(Name = SwarmHttpConsts.ContentTypeHeader)] string contentType,
                        [FromHeader(Name = BeehiveHttpConsts.SwarmCompactLevelHeader)] ushort compactLevel = 0,
                        [FromHeader(Name = SwarmHttpConsts.SwarmEncryptHeader)] bool encrypt = false,
                        [FromHeader(Name = SwarmHttpConsts.SwarmPinningHeader)] bool pinContent = false,
                        [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyLevelHeader)] RedundancyLevel redundancyLevel = RedundancyLevel.None,
                        [FromHeader(Name = SwarmHttpConsts.SwarmCollectionHeader)] bool isDirectory = false,
                        [FromHeader(Name = SwarmHttpConsts.SwarmIndexDocumentHeader)] string? indexDocument = null,
                        [FromHeader(Name = SwarmHttpConsts.SwarmErrorDocumentHeader)] string? errorDocument = null) =>
                    handler.UploadBzzAsync(
                        name,
                        batchId,
                        compactLevel,
                        encrypt,
                        pinContent,
                        redundancyLevel,
                        contentType,
                        isDirectory,
                        indexDocument,
                        errorDocument))
                .AcceptsUnrestricted<Stream>(
                    BeehiveHttpConsts.ApplicationOctetStreamContentType,
                    BeehiveHttpConsts.ApplicationTarContentType,
                    BeehiveHttpConsts.MultiPartFormDataContentType,
                    BeehiveHttpConsts.AnyContentType)
                .NotProduces200()
                .Produces<ChunkReferenceDto>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status402PaymentRequired)
                .Produces(StatusCodes.Status404NotFound);
            
            //chainstate
            builder.MapGet("/chainstate",
                    (IChainstateApiHandler handler) => handler.GetChainstate())
                .Produces<ChainStateDto>();
            
            //chunks
            builder.MapGet("/chunks/{hash}",
                    (IChunksApiHandler handler,
                            [FromRoute] SwarmHash hash) =>
                        handler.DownloadChunkAsync(hash))
                .Produces<Stream>(StatusCodes.Status200OK, BeehiveHttpConsts.BinaryOctetStreamContentType)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapHead("/chunks/{hash}",
                    (IChunksApiHandler handler,
                            [FromRoute] SwarmHash hash) =>
                        handler.GetChunkHeadersAsync(hash))
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapPost("/chunks",
                    (IChunksApiHandler handler,
                            [FromBody] Stream dataStream,
                            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader)] PostageBatchId? batchId,
                            [FromHeader(Name = SwarmHttpConsts.SwarmPostageStampHeader)] PostageStamp? postageStamp) =>
                        handler.UploadChunkAsync(dataStream, batchId, postageStamp))
                .Accepts<Stream>(BeehiveHttpConsts.ApplicationOctetStreamContentType)
                .FilterRequestSizeLimit(SwarmCac.SpanDataSize)
                .FilterRequireAtLeastOneHeader(
                    SwarmHttpConsts.SwarmPostageBatchIdHeader,
                    SwarmHttpConsts.SwarmPostageStampHeader)
                .NotProduces200()
                .Produces<ChunkReferenceDto>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status402PaymentRequired)
                .Produces(StatusCodes.Status404NotFound);
            
            if (!hasBeePrefix)
            {
                //** OBSOLETE: used by BeeTurbo **
                builder.MapPost("/chunks/bulk-upload",
                    (IChunksApiHandler handler,
                            [FromBody] Stream dataStream,
                            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader)] PostageBatchId batchId) =>
                        handler.BulkUploadChunksAsync(dataStream, batchId))
                    .Accepts<Stream>(BeehiveHttpConsts.ApplicationOctetStreamContentType)
                    .FilterRequestSizeLimit(100 * 1024 * 1024) //100MB
                    .NotProduces200()
                    .Produces(StatusCodes.Status201Created)
                    .Produces(StatusCodes.Status400BadRequest)
                    .Produces(StatusCodes.Status402PaymentRequired)
                    .Produces(StatusCodes.Status404NotFound)
                    .IsDeprecated("Use /ev1/chunks/bulk-upload instead");
            
                builder.MapPost("/ev1/chunks/bulk-upload",
                    (IChunksApiHandler handler,
                            [FromBody] Stream dataStream,
                            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader)] PostageBatchId batchId) =>
                        handler.BulkUploadChunksAsync(dataStream, batchId))
                    .Accepts<Stream>(BeehiveHttpConsts.ApplicationOctetStreamContentType)
                    .FilterRequestSizeLimit(100 * 1024 * 1024) //100MB
                    .NotProduces200()
                    .Produces(StatusCodes.Status201Created)
                    .Produces(StatusCodes.Status400BadRequest)
                    .Produces(StatusCodes.Status402PaymentRequired)
                    .Produces(StatusCodes.Status404NotFound);
            }
            
            //feeds
            builder.MapGet("/feeds/{owner}/{topic}",
                (IFeedsApiHandler handler,
                        [FromRoute] EthAddress owner,
                        [FromRoute] SwarmFeedTopic topic,
                        [FromQuery] long? at,
                        [FromQuery] ulong? after,
                        [FromQuery] byte? afterLevel,
                        [FromQuery] SwarmFeedType type = SwarmFeedType.Sequence,
                        [FromHeader(Name = SwarmHttpConsts.SwarmOnlyRootChunkHeader)] bool onlyRootChunk = false,
                        [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyStrategyHeader)] RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data, 
                        [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyFallbackModeHeader)] bool redundancyStrategyFallback = true,
                        [FromHeader(Name = SwarmHttpConsts.SwarmFeedLegacyResolveHeader)] bool resolveLegacyPayload = false) =>
                    handler.FindFeedUpdateAsync(owner, topic, at, after, afterLevel, type, onlyRootChunk, redundancyStrategy, redundancyStrategyFallback, resolveLegacyPayload))
                .Produces<Stream>(StatusCodes.Status200OK, BeehiveHttpConsts.ApplicationOctetStreamContentType)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapPost("/feeds/{owner}/{topic}",
                (IFeedsApiHandler handler,
                        [FromRoute] EthAddress owner,
                        [FromRoute] SwarmFeedTopic topic,
                        [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader)] PostageBatchId batchId,
                        [FromHeader(Name = BeehiveHttpConsts.SwarmCompactLevelHeader)] ushort compactLevel = 0,
                        [FromHeader(Name = SwarmHttpConsts.SwarmPinningHeader)] bool pinContent = false,
                        [FromQuery] SwarmFeedType type = SwarmFeedType.Sequence) =>
                    handler.CreateFeedRootManifestAsync(owner, topic, type, batchId, compactLevel, pinContent))
                .NotProduces200()
                .Produces<ChunkReferenceDto>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status402PaymentRequired)
                .Produces(StatusCodes.Status404NotFound);
            
            //health
            builder.MapGet("/health",
                    (IHealthApiHandler handler) => handler.GetHealthStatus())
                .Produces<HealthDto>();
            
            //node
            builder.MapGet("/node",
                    (INodeApiHandler handler) => handler.GetNodeStatus())
                .Produces<NodeDto>();
            
            //pins
            builder.MapGet("/pins",
                    (IPinsApiHandler handler) => handler.GetPinsBeeAsync())
                .Produces<BeePinsDto>();
            
            if (!hasBeePrefix)
            {
                builder.MapGet("/ev1/pins",
                        (IPinsApiHandler handler,
                                [Range(0, int.MaxValue)] int page = 0,
                                [Range(1, 10000)] int take = 500) =>
                            handler.GetPinsBeehiveAsync(page, take))
                    .Produces<IEnumerable<BeehivePinDto>>();
            }
            
            builder.MapGet("/pins/{reference}",
                    (IPinsApiHandler handler,
                            [FromRoute] SwarmReference reference) =>
                        handler.GetPinStatusBeeAsync(reference))
                .Produces<ChunkReferenceDto>()
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            if (!hasBeePrefix)
            {
                builder.MapGet("/ev1/pins/{reference}",
                        (IPinsApiHandler handler,
                                [FromRoute] SwarmReference reference) =>
                            handler.GetPinStatusBeehiveAsync(reference))
                    .Produces<BeehivePinDto>()
                    .Produces(StatusCodes.Status400BadRequest)
                    .Produces(StatusCodes.Status404NotFound);
            }
            
            builder.MapPost("/pins/{reference}",
                    (IPinsApiHandler handler,
                            [FromRoute] SwarmReference reference) =>
                        handler.CreatePinBeeAsync(reference))
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            if (!hasBeePrefix)
            {
                builder.MapPost("/ev1/pins/{reference}",
                        (IPinsApiHandler handler,
                                [FromRoute] SwarmReference reference) =>
                            handler.CreatePinBeehiveAsync(reference))
                    .Produces(StatusCodes.Status200OK);
            }
            
            builder.MapDelete("/pins/{reference}",
                    (IPinsApiHandler handler,
                            [FromRoute] SwarmReference reference) =>
                        handler.DeletePinAsync(reference))
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            //readiness
            builder.MapGet("/readiness",
                    (IReadinessApiHandler handler) => handler.GetReadinessStatus())
                .Produces<ReadinessDto>();
            
            //soc
            builder.MapGet("/soc/{owner}/{id}",
                    (ISocApiHandler handler,
                            [FromRoute] EthAddress owner,
                            [FromRoute] SwarmSocIdentifier id,
                            [FromHeader(Name = SwarmHttpConsts.SwarmOnlyRootChunkHeader)] bool onlyRootChunk = false,
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyStrategyHeader)] RedundancyStrategy redundancyStrategy = RedundancyStrategy.Data, 
                            [FromHeader(Name = SwarmHttpConsts.SwarmRedundancyFallbackModeHeader)] bool redundancyStrategyFallback = true) =>
                        handler.ResolveSocAsync(owner, id, onlyRootChunk, redundancyStrategy, redundancyStrategyFallback))
                .Produces<Stream>(StatusCodes.Status200OK, BeehiveHttpConsts.ApplicationOctetStreamContentType)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapPost("/soc/{owner}/{id}",
                (ISocApiHandler handler,
                    [FromRoute] EthAddress owner,
                    [FromRoute] SwarmSocIdentifier id,
                    [FromBody] Stream dataStream,
                    [FromQuery(Name = "sig"), Required] string signature,
                    [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader)] PostageBatchId? batchId,
                    [FromHeader(Name = SwarmHttpConsts.SwarmPostageStampHeader)] PostageStamp? postageStamp,
                    [FromHeader(Name = SwarmHttpConsts.SwarmPinningHeader)] bool pinContent = false) =>
                    handler.UploadSocAsync(
                        owner,
                        id,
                        signature,
                        batchId,
                        postageStamp,
                        dataStream,
                        pinContent))
                .Accepts<Stream>(BeehiveHttpConsts.ApplicationOctetStreamContentType)
                .FilterRequestSizeLimit(SwarmCac.SpanDataSize)
                .FilterRequireAtLeastOneHeader(
                    SwarmHttpConsts.SwarmPostageBatchIdHeader,
                    SwarmHttpConsts.SwarmPostageStampHeader)
                .NotProduces200()
                .Produces<ChunkReferenceDto>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status401Unauthorized)
                .Produces(StatusCodes.Status402PaymentRequired)
                .Produces(StatusCodes.Status404NotFound);
                
            //stamps
            builder.MapGet("/stamps",
                (IStampsApiHandler handler) =>
                    handler.GetOwnedPostageBatchesAsync())
                .Produces<PostageBatchStampListDto>();
            
            builder.MapGet("/stamps/{batchId}",
                    (IStampsApiHandler handler,
                            [FromRoute] PostageBatchId batchId) =>
                        handler.GetPostageBatchAsync(batchId))
                .Produces<PostageBatchDto>()
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapGet("/stamps/{batchId}/buckets",
                    (IStampsApiHandler handler,
                            [FromRoute] PostageBatchId batchId) =>
                        handler.GetPostageBatchBucketsAsync(batchId))
                .Produces<PostageBatchBucketsDto>()
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapPatch("/stamps/dilute/{batchId}/{depth}",
                    (IStampsApiHandler handler,
                            [FromRoute] PostageBatchId batchId,
                            [FromRoute] int depth,
                            [FromHeader(Name = SwarmHttpConsts.GasLimitHeader)] ulong? gasLimit = null,
                            [FromHeader(Name = SwarmHttpConsts.GasPriceHeader)] XDaiValue? gasPrice = null) =>
                        handler.DilutePostageBatchAsync(batchId, depth, gasLimit, gasPrice))
                .NotProduces200()
                .Produces<PostageBatchIdWithTxHashDto>(StatusCodes.Status202Accepted)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status429TooManyRequests);
            
            builder.MapPatch("/stamps/topup/{batchId}/{amount}",
                    (IStampsApiHandler handler,
                            [FromRoute] PostageBatchId batchId,
                            [FromRoute] BzzValue amount,
                            [FromHeader(Name = SwarmHttpConsts.GasLimitHeader)] ulong? gasLimit = null,
                            [FromHeader(Name = SwarmHttpConsts.GasPriceHeader)] XDaiValue? gasPrice = null) =>
                        handler.TopUpPostageBatchAsync(batchId, amount, gasLimit, gasPrice))
                .NotProduces200()
                .Produces<PostageBatchIdWithTxHashDto>(StatusCodes.Status202Accepted)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status402PaymentRequired)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status429TooManyRequests);
            
            builder.MapPost("/stamps/{amount}/{depth}",
                    (IStampsApiHandler handler,
                            [FromRoute] BzzValue amount,
                            [FromRoute] int depth,
                            [FromQuery] string? label = null,
                            [FromHeader(Name = SwarmHttpConsts.ImmutableHeader)] bool immutable = false,
                            [FromHeader(Name = SwarmHttpConsts.GasLimitHeader)] ulong? gasLimit = null,
                            [FromHeader(Name = SwarmHttpConsts.GasPriceHeader)] XDaiValue? gasPrice = null) =>
                        handler.BuyPostageBatchAsync(amount, depth, label, immutable, gasLimit, gasPrice))
                .NotProduces200()
                .Produces<PostageBatchIdWithTxHashDto>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status429TooManyRequests);
        }
    }
}
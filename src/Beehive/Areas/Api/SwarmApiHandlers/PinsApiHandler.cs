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
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Tasks.Trigger;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Extensions;
using Etherna.SwarmSdk.Models;
using Hangfire;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public sealed class PinsApiHandler(
        IBackgroundJobClient backgroundJobClient,
        IBeehiveDbContext dbContext,
        IPinChunksTask pinChunksTask,
        IPinIntegrityService pinIntegrityService,
        IPinService pinService)
        : IPinsApiHandler
    {
        // Consts.
        private const string AllPinsCheckLockId = "all";
        private const string AllPinsCheckRunningErrorMessage = "pin integrity check of all pins already running";

        // Methods.
        public Task<IResult> CheckPinsAsync(SwarmReference? reference, HttpContext httpContext) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                ArgumentNullException.ThrowIfNull(httpContext);
                var cancellationToken = httpContext.RequestAborted;

                // No check while a deprecated store holds chunks: the check reads the chunks keyed by hash only.
                await pinIntegrityService.VerifyPinsCanBeCheckedAsync(cancellationToken);

                // One check of every pin at a time, as bee: it reads every pinned chunk.
                if (!reference.HasValue)
                {
                    var allPinsCheckLease = await dbContext.TryAcquireResourceLockAsync(
                        ResourceLockNamespaces.PinChecks,
                        AllPinsCheckLockId);
                    if (allPinsCheckLease is null)
                        return Results.Json(
                            new BeeErrorDto(StatusCodes.Status429TooManyRequests, AllPinsCheckRunningErrorMessage),
                            CommonConsts.SwarmJsonSerializerOptions,
                            statusCode: StatusCodes.Status429TooManyRequests);
                    httpContext.Response.RegisterForDisposeAsync(allPinsCheckLease);
                }

                // A stream with a json object per line, written as each reference is checked, as bee: for the
                // given reference, or for every pinned one, by the age of its first pin.
                return Results.Stream(async stream =>
                    {
                        IEnumerable<SwarmReference> references = reference.HasValue ?
                            [reference.Value] :
                            (await dbContext.ChunkPins.QueryElementsAsync(elements =>
                                elements.Where(p => p.Reference.HasValue)
                                    .OrderBy(p => p.CreationDateTime)
                                    .Select(p => p.Reference!.Value)
                                    .ToListAsync(cancellationToken))).Distinct();

                        foreach (var pinnedReference in references)
                        {
                            // A content uploaded more than once with its pin has a pin per upload. Each one is
                            // checked, and takes its place among the references of the chunks; they hold the same
                            // content, so the reference has one line, the check of its last pin.
                            var pins = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                                elements.Where(p => p.Reference == pinnedReference)
                                    .OrderBy(p => p.CreationDateTime)
                                    .ToListAsync(cancellationToken));
                            PinCheckResult? checkResult = null;
                            foreach (var pin in pins)
                                checkResult = await pinIntegrityService.CheckPinAsync(pin, cancellationToken);

                            // A reference beehive doesn't pin answers every count at zero, as bee answers one it
                            // doesn't pin. In a check of every pin, one whose pins were deleted meanwhile has no line.
                            if (checkResult is null && !reference.HasValue)
                                continue;
                            await JsonSerializer.SerializeAsync(
                                stream,
                                new BeePinCheckDto(
                                    pinnedReference,
                                    checkResult?.MissingChunks.Count ?? 0,
                                    checkResult?.InvalidChunks.Count ?? 0,
                                    checkResult?.TotalChunks ?? 0),
                                CommonConsts.SwarmJsonSerializerOptions,
                                cancellationToken);
                            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
                            await stream.FlushAsync(cancellationToken);
                        }
                    },
                    BeehiveHttpConsts.ApplicationNdjsonContentType);
            });

        public Task<IResult> CreatePinBeeAsync(SwarmReference reference) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                // Try to create pin. A reference already pinned is bee's 200, a new pin its 201: bee's status bodies.
                if (await CreatePinHelperAsync(reference, false))
                    return Results.Json(
                        new BeeStatusDto(StatusCodes.Status200OK, "OK"),
                        CommonConsts.SwarmJsonSerializerOptions);
            
                // Verify pin result.
                var pin = await dbContext.ChunkPins.FindOneAsync(p => p.Reference == reference);
            
                if (pin.IsSucceeded)
                    return Results.Json(
                        new BeeStatusDto(StatusCodes.Status201Created, "Created"),
                        CommonConsts.SwarmJsonSerializerOptions,
                        statusCode: StatusCodes.Status201Created);
            
                // If not succeeded, reverse partial pin removing it.
                await pinService.TryDeletePinAsync(reference);
            
                throw new KeyNotFoundException();
            });

        public Task<IResult> CreatePinBeehiveAsync(SwarmReference reference) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                await CreatePinHelperAsync(reference, true);
                return Results.Ok();
            });

        public Task<IResult> DeletePinAsync(SwarmReference reference) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
                await pinService.TryDeletePinAsync(reference)
                    ? Results.Ok()
                    : Results.NotFound());

        public Task<IResult> GetPinsBeeAsync() =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var pinnedReferences = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                    elements.Where(p => p.IsProcessed && !p.MissingChunks.Any())
                        .Where(p => p.Reference.HasValue)
                        .Select(p => p.Reference!.Value)
                        .Distinct()
                        .ToListAsync());
                return Results.Json(
                    new BeePinsDto(pinnedReferences),
                    CommonConsts.SwarmJsonSerializerOptions);
            });

        public Task<IResult> GetPinsBeehiveAsync(int page, int take) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var pins = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                    elements.Where(p => p.Reference.HasValue)
                        .PaginateDescending(p => p.CreationDateTime, page, take)
                        .ToListAsync());
                return Results.Json(
                    pins.Select(p => new BeehivePinDto(
                        p.Reference!.Value,
                        p.CreationDateTime,
                        p.MissingChunks,
                        p.IsProcessed,
                        p.IsSucceeded,
                        p.TotPinnedChunks)),
                    CommonConsts.SwarmJsonSerializerOptions);
            });

        public Task<IResult> GetPinStatusBeeAsync(SwarmReference reference) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var pin = await dbContext.ChunkPins.TryFindOneAsync(p =>
                    p.Reference == reference && p.IsProcessed && !p.MissingChunks.Any());
                if (pin is null)
                    throw new KeyNotFoundException();
                return Results.Json(
                    new ChunkReferenceDto(reference),
                    CommonConsts.SwarmJsonSerializerOptions);
            });

        public Task<IResult> GetPinStatusBeehiveAsync(SwarmReference reference) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Reference == reference);
                if (pin is null)
                    throw new KeyNotFoundException();
                return Results.Json(
                    new BeehivePinDto(
                        pin.Reference!.Value,
                        pin.CreationDateTime,
                        pin.MissingChunks,
                        pin.IsProcessed,
                        pin.IsSucceeded,
                        pin.TotPinnedChunks),
                    CommonConsts.SwarmJsonSerializerOptions);
            });

        // Helpers.
        /// <returns>True when the reference was already pinned, and nothing was done</returns>
        private async Task<bool> CreatePinHelperAsync(SwarmReference chunkRef, bool runBackgroundTask)
        {
            // Try find pin with this hash.
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Reference == chunkRef);
            
            // If doesn't exist create it.
            if (pin is null)
            {
                pin = new ChunkPin(chunkRef);
                await dbContext.ChunkPins.CreateAsync(pin);
            }
            else
            {
                if (pin.IsSucceeded)
                    return true;
            }
            
            if (runBackgroundTask)
            {
                backgroundJobClient.Enqueue<IPinChunksTask>(
                    t => t.RunAsync(pin.Id, CancellationToken.None));
            }
            else
            {
                await pinChunksTask.RunAsync(pin.Id, CancellationToken.None);
            }

            return false;
        }
    }
}
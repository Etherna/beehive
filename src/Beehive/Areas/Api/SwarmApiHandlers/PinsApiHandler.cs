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
using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Tasks.Trigger;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Models;
using Etherna.MongoDB.Driver.Linq;
using Etherna.MongODM.Core.Extensions;
using Etherna.MongODM.Core.Serialization.Modifiers;
using Hangfire;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public sealed class PinsApiHandler(
        IBackgroundJobClient backgroundJobClient,
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        IPinService pinService,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IPinsApiHandler
    {
        public Task<IResult> CreatePinBeeAsync(SwarmReference reference) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                // Try to create pin.
                await CreatePinHelperAsync(reference, false);
            
                // Verify pin result.
                var pin = await dbContext.ChunkPins.FindOneAsync(p => p.Reference == reference);
            
                if (pin.IsSucceeded)
                    return Results.Created();
            
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
                return Results.Json(new BeePinsDto(pinnedReferences));
            });

        public Task<IResult> GetPinsBeehiveAsync(int page, int take) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var pins = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                    elements.Where(p => p.Reference.HasValue)
                        .PaginateDescending(p => p.CreationDateTime, page, take)
                        .ToListAsync());
                return Results.Json(pins.Select(p => new BeehivePinDto(
                    p.Reference!.Value,
                    p.CreationDateTime,
                    p.MissingChunks,
                    p.IsProcessed,
                    p.IsSucceeded,
                    p.TotPinnedChunks)));
            });

        public Task<IResult> GetPinStatusBeeAsync(SwarmReference reference) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var pin = await dbContext.ChunkPins.TryFindOneAsync(p =>
                    p.Reference == reference && p.IsProcessed && !p.MissingChunks.Any());
                if (pin is null)
                    throw new KeyNotFoundException();
                return Results.Json(new ChunkReferenceDto(reference));
            });

        public Task<IResult> GetPinStatusBeehiveAsync(SwarmReference reference) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Reference == reference);
                if (pin is null)
                    throw new KeyNotFoundException();
                return Results.Json(new BeehivePinDto(
                    pin.Reference!.Value,
                    pin.CreationDateTime,
                    pin.MissingChunks,
                    pin.IsProcessed,
                    pin.IsSucceeded,
                    pin.TotPinnedChunks));
            });

        // Helpers.
        private async Task CreatePinHelperAsync(SwarmReference chunkRef, bool runBackgroundTask)
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
                    return;
            }
            
            if (runBackgroundTask)
            {
                backgroundJobClient.Enqueue<IPinChunksTask>(
                    t => t.RunAsync(pin.Id));
            }
            else
            {
                var task = new PinChunksTask(
                    beeNodeLiveManager,
                    pinService,
                    dbContext,
                    serializerModifierAccessor);
                await task.RunAsync(pin.Id);
            }
        }
    }
}
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
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class PinsControllerService(
        IBackgroundJobClient backgroundJobClient,
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        IPinService pinService,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IPinsControllerService
    {
        public async Task<IActionResult> CreatePinBeeAsync(SwarmReference reference)
        {
            // Try create pin.
            await CreatePinHelperAsync(reference, false);
            
            // Verify pin result.
            var pin = await dbContext.ChunkPins.FindOneAsync(p => p.Reference == reference);
            
            if (pin.IsSucceeded)
                return new CreatedResult();
            
            // If not succeeded, reverse partial pin removing it.
            await DeletePinAsync(reference);
            
            return new BeeNotFoundResult("pin collection failed");
        }

        public Task CreatePinBeehiveAsync(SwarmReference reference) =>
            CreatePinHelperAsync(reference, true);

        public async Task<IActionResult> DeletePinAsync(SwarmReference reference) =>
            await pinService.TryDeletePinAsync(reference)
                ? new OkResult()
                : new NotFoundResult();

        public async Task<IActionResult> GetPinsBeeAsync()
        {
            var pinnedReferences = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                elements.Where(p => p.IsProcessed && !p.MissingChunks.Any())
                    .Where(p => p.Reference.HasValue)
                    .Select(p => p.Reference!.Value)
                    .Distinct()
                    .ToListAsync());
            return new JsonResult(new BeePinsDto(pinnedReferences));
        }

        public async Task<IActionResult> GetPinsBeehiveAsync(int page, int take)
        {
            var pins = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                elements.Where(p => p.Reference.HasValue)
                    .PaginateDescending(p => p.CreationDateTime, page, take)
                    .ToListAsync());
            return new JsonResult(pins.Select(p => new BeehivePinDto(
                p.Reference!.Value,
                p.CreationDateTime,
                p.MissingChunks,
                p.IsProcessed,
                p.IsSucceeded,
                p.TotPinnedChunks)));
        }

        public async Task<IActionResult> GetPinStatusBeeAsync(SwarmReference reference)
        {
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p =>
                p.Reference == reference && p.IsProcessed && !p.MissingChunks.Any());
            if (pin is null)
                return new BeeNotFoundResult();
            return new JsonResult(new ChunkReferenceDto(reference));
        }

        public async Task<IActionResult> GetPinStatusBeehiveAsync(SwarmReference reference)
        {
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Reference == reference);
            if (pin is null)
                return new BeeNotFoundResult();
            return new JsonResult(new BeehivePinDto(
                pin.Reference!.Value,
                pin.CreationDateTime,
                pin.MissingChunks,
                pin.IsProcessed,
                pin.IsSucceeded,
                pin.TotPinnedChunks));
        }

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
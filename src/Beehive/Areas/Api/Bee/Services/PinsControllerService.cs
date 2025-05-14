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
using Etherna.Beehive.Services.Tasks;
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
        IChunkPinService chunkPinService,
        IBeehiveDbContext dbContext,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IPinsControllerService
    {
        public Task CreatePinBeeAsync(string hash) =>
            CreatePinHelperAsync(hash, false);

        public Task CreatePinBeehiveAsync(string hash) =>
            CreatePinHelperAsync(hash, true);

        public async Task<IActionResult> GetPinsBeeAsync()
        {
            var pinnedHashes = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                elements.Where(p => p.IsSucceeded)
                    .Where(p => p.Hash.HasValue)
                    .Select(p => p.Hash!.Value)
                    .ToListAsync());
            return new JsonResult(new BeePinsDto(pinnedHashes));
        }

        public async Task<IActionResult> GetPinsBeehiveAsync(int page, int take)
        {
            var pins = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                elements.Where(p => p.Hash.HasValue)
                    .PaginateDescending(p => p.CreationDateTime, page, take)
                    .ToListAsync());
            return new JsonResult(pins.Select(p => new BeehivePinDto(
                p.Hash!.Value,
                p.CreationDateTime,
                p.EncryptionKey,
                p.MissingChunks,
                p.IsProcessed,
                p.RecursiveEncryption,
                p.IsSucceeded,
                p.TotPinnedChunks)));
        }

        public async Task<IActionResult> GetPinStatusBeeAsync(SwarmHash hash)
        {
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Hash == hash && p.IsSucceeded);
            if (pin is null)
                return new BeeNotFoundResult();
            return new JsonResult(new SimpleChunkReferenceDto(hash));
        }

        public async Task<IActionResult> GetPinStatusBeehiveAsync(SwarmHash hash)
        {
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Hash == hash);
            if (pin is null)
                return new BeeNotFoundResult();
            return new JsonResult(new BeehivePinDto(
                pin.Hash!.Value,
                pin.CreationDateTime,
                pin.EncryptionKey,
                pin.MissingChunks,
                pin.IsProcessed,
                pin.RecursiveEncryption,
                pin.IsSucceeded,
                pin.TotPinnedChunks));
        }

        // Helpers.
        private async Task CreatePinHelperAsync(string hash, bool runBackgroundTask)
        {
            /* Pin created from this API are always recursive on chunks. */
            // Try find recursive pin with this hash.
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Hash == hash);
            
            // If doesn't exist create it.
            if (pin is null)
            {
                pin = new ChunkPin(hash);
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
                    t => t.RunAsync(pin.Id, null!));
            }
            else
            {
                var task = new PinChunksTask(
                    beeNodeLiveManager,
                    chunkPinService,
                    dbContext,
                    serializerModifierAccessor);
                await task.RunAsync(pin.Id, null!);
            }
        }
    }
}
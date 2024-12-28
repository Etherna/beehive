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
using Etherna.Beehive.Domain;
using Etherna.MongoDB.Driver.Linq;
using Etherna.MongODM.Core.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class PinsControllerService(
        IBeehiveDbContext dbContext)
        : IPinsControllerService
    {
        public async Task<BeePinsDto> GetPinsBeeAsync()
        {
            var pinnedHashes = await dbContext.ChunkPins.QueryElementsAsync(
                q => q.Where(p => p.IsSucceeded)
                    .Select(p => p.Hash)
                    .ToListAsync());
            return new BeePinsDto(pinnedHashes);
        }

        public async Task<IEnumerable<BeehivePinDto>> GetPinsBeehiveAsync(int page, int take)
        {
            var pins = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                elements.PaginateDescending(n => n.CreationDateTime, page, take)
                    .ToListAsync());
            return pins.Select(p => new BeehivePinDto(
                p.Hash,
                p.MissingChunks,
                p.IsProcessed,
                p.IsRecursive,
                p.IsSucceeded,
                p.TotPinnedChunks));
        }
    }
}
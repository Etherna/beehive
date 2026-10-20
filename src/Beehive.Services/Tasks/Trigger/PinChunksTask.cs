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

using Etherna.Beehive.Domain;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Trigger
{
    public class PinChunksTask(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        ILogger<PinChunksTask> logger,
        IPinService pinService,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IPinChunksTask
    {
        // Methods.
        public async Task RunAsync(string chunkPinId)
        {
            await using var chunkStore =
                new BeehiveChunkStore(beeNodeLiveManager, dbContext, logger, serializerModifierAccessor);
            await pinService.PinChunksAsync(chunkPinId, chunkStore);
        }
    }
}
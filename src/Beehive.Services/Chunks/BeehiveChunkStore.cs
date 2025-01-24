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

using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Stores;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Chunks
{
    public sealed class BeehiveChunkStore(
        IBeeNodeLiveManager beeNodeLiveManager,
        IDbChunkStore dbChunkStore)
        : ReadOnlyChunkStoreBase, IBeehiveChunkStore
    {
        protected override async Task<SwarmChunk> LoadChunkAsync(SwarmHash hash)
        {
            // Try load from db first.
            var chunk = await dbChunkStore.TryGetAsync(hash);
            if (chunk is not null)
                return chunk;
            
            // If it's not found, search on a healthy node.
            var node = await beeNodeLiveManager.SelectHealthyNodeAsync();
            var beeClientChunkStore = new BeeClientChunkStore(node.Client);
            return await beeClientChunkStore.GetAsync(hash);
        }
    }
}
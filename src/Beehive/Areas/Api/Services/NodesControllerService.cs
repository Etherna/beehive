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
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Exceptions;
using Etherna.BeeNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Services
{
    internal sealed class NodesControllerService(
        IBeeNodeLiveManager beeNodeLiveManager)
        : INodesControllerService_old
    {
        // Methods.
        public async Task<bool> CheckResourceAvailabilityFromNodeAsync(string id, SwarmHash hash)
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            return await beeNodeInstance.Client.IsContentRetrievableAsync(hash);
        }

        public async Task<bool> ForceFullStatusRefreshAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            return await beeNodeInstance.TryRefreshStatusAsync(true);
        }

        public IEnumerable<BeeNodeStatusDto> GetAllBeeNodeLiveStatus() =>
            beeNodeLiveManager.AllNodes.Select(n => new BeeNodeStatusDto(n.Id, n.Status));

        public async Task<BeeNodeStatusDto> GetBeeNodeLiveStatusAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            return new BeeNodeStatusDto(beeNodeInstance.Id, beeNodeInstance.Status);
        }

        public async Task<PostageBatchDto> GetPostageBatchDetailsAsync(string id, PostageBatchId batchId)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            try
            {
                var postageBatch = await beeNodeInstance.Client.GetPostageBatchAsync(batchId);
                return new PostageBatchDto(postageBatch);
            }
            catch (BeeNetApiException ex) when (ex.StatusCode == 400)
            {
                throw new KeyNotFoundException();
            }
        }

        public async Task<IEnumerable<PostageBatchDto>> GetPostageBatchesByNodeAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            var batches = await beeNodeInstance.Client.GetOwnedPostageBatchesByNodeAsync();
            return batches.Select(b => new PostageBatchDto(b));
        }

        public async Task ReuploadResourceToNetworkFromNodeAsync(string id, SwarmHash hash)
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            await beeNodeInstance.Client.ReuploadContentAsync(hash);
        }
    }
}

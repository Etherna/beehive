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
using Etherna.BeeNet.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Services
{
    public interface INodesControllerService_old
    {
        Task<bool> CheckResourceAvailabilityFromNodeAsync(string id, SwarmHash hash);
        Task<bool> ForceFullStatusRefreshAsync(string id);
        IEnumerable<BeeNodeStatusDto> GetAllBeeNodeLiveStatus();
        Task<BeeNodeStatusDto> GetBeeNodeLiveStatusAsync(string id);
        Task<PostageBatchDto> GetPostageBatchDetailsAsync(string id, PostageBatchId batchId);
        Task<IEnumerable<PostageBatchDto>> GetPostageBatchesByNodeAsync(string id);
        Task ReuploadResourceToNetworkFromNodeAsync(string id, SwarmHash hash);
    }
}
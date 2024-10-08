// Copyright 2021-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.InputModels;
using Etherna.BeeNet.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface INodesControllerService
    {
        Task<BeeNodeDto> AddBeeNodeAsync(BeeNodeInput input);
        Task<bool> CheckResourceAvailabilityFromNodeAsync(string id, SwarmHash hash);
        Task DeletePinAsync(string id, SwarmHash hash);
        Task<BeeNodeDto> FindByIdAsync(string id);
        Task<bool> ForceFullStatusRefreshAsync(string id);
        IEnumerable<BeeNodeStatusDto> GetAllBeeNodeLiveStatus();
        Task<BeeNodeStatusDto> GetBeeNodeLiveStatusAsync(string id);
        Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take);
        Task<PinnedResourceDto> GetPinDetailsAsync(string id, SwarmHash hash);
        Task<IEnumerable<SwarmHash>> GetPinsByNodeAsync(string id);
        Task<PostageBatchDto> GetPostageBatchDetailsAsync(string id, PostageBatchId batchId);
        Task<IEnumerable<PostageBatchDto>> GetPostageBatchesByNodeAsync(string id);
        Task NotifyPinningOfUploadedContentAsync(string id, SwarmHash hash);
        Task RemoveBeeNodeAsync(string id);
        Task ReuploadResourceToNetworkFromNodeAsync(string id, SwarmHash hash);
        Task UpdateNodeConfigAsync(string id, UpdateNodeConfigInput config);
    }
}
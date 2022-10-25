//   Copyright 2021-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Utilities.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Utilities
{
    public interface IBeeNodeLiveManager
    {
        // Properties.
        IEnumerable<BeeNodeLiveInstance> AllNodes { get; }
        ChainState? ChainState { get; }
        IEnumerable<BeeNodeLiveInstance> HealthyNodes { get; }

        // Methods.
        Task<BeeNodeLiveInstance> AddBeeNodeAsync(BeeNode beeNode);
        Task<BeeNodeLiveInstance> GetBeeNodeLiveInstanceAsync(string nodeId);
        BeeNodeLiveInstance GetBeeNodeLiveInstanceByOwnedPostageBatch(string batchId);
        IEnumerable<BeeNodeLiveInstance> GetBeeNodeLiveInstancesByPinnedContent(string hash, bool requireAliveNodes);
        Task LoadAllNodesAsync();
        bool RemoveBeeNode(string nodeId);
        void StartHealthHeartbeat();
        void StopHealthHeartbeat();
        Task<BeeNodeLiveInstance?> TrySelectHealthyNodeAsync(
            BeeNodeSelectionMode mode,
            string? selectionContext = null,
            Func<BeeNodeLiveInstance, Task<bool>>? isValidPredicate = null);
    }
}
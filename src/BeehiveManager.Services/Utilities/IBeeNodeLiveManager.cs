// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Utilities.Models;
using Etherna.BeeNet.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChainState = Etherna.BeehiveManager.Services.Utilities.Models.ChainState;

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
        BeeNodeLiveInstance GetBeeNodeLiveInstanceByOwnedPostageBatch(PostageBatchId batchId);
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
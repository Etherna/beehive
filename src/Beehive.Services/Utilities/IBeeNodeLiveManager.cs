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

using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.BeeNet.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ChainState = Etherna.Beehive.Services.Utilities.Models.ChainState;

namespace Etherna.Beehive.Services.Utilities
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
        Task LoadAllNodesAsync();
        bool RemoveBeeNode(string nodeId);
        Task<BeeNodeLiveInstance> SelectDownloadNodeAsync(SwarmHash hash);
        Task<BeeNodeLiveInstance> SelectHealthyNodeAsync(
            BeeNodeSelectionMode mode = BeeNodeSelectionMode.RoundRobin,
            string? selectionContext = null,
            Func<BeeNodeLiveInstance, Task<bool>>? isValidPredicate = null);
        void StartHealthHeartbeat();
        void StopHealthHeartbeat();
        Task<BeeNodeLiveInstance?> TrySelectHealthyNodeAsync(
            BeeNodeSelectionMode mode = BeeNodeSelectionMode.RoundRobin,
            string? selectionContext = null,
            Func<BeeNodeLiveInstance, Task<bool>>? isValidPredicate = null);
    }
}
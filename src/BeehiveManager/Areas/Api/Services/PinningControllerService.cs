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

using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Tasks;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using Hangfire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class PinningControllerService : IPinningControllerService
    {
        private const string PinNewContentSelectionContext = "pinNewContent";

        // Fields.
        private readonly IBackgroundJobClient backgroundJobClient;
        private readonly IBeeNodeLiveManager beeNodeLiveManager;
        private readonly IBeehiveDbContext dbContext;

        // Constructor.
        public PinningControllerService(
            IBackgroundJobClient backgroundJobClient,
            IBeeNodeLiveManager beeNodeLiveManager,
            IBeehiveDbContext dbContext)
        {
            this.backgroundJobClient = backgroundJobClient;
            this.beeNodeLiveManager = beeNodeLiveManager;
            this.dbContext = dbContext;
        }

        // Methods.
        public async Task<IEnumerable<BeeNodeDto>> FindBeeNodesPinningContentAsync(string hash, bool requireAliveNodes)
        {
            var beeNodeLiveInstances = beeNodeLiveManager.GetBeeNodeLiveInstancesByPinnedContent(hash, requireAliveNodes);
            var beeNodes = new List<BeeNode>();
            foreach (var instance in beeNodeLiveInstances)
                beeNodes.Add(await dbContext.BeeNodes.FindOneAsync(instance.Id));

            return beeNodes.Select(n => new BeeNodeDto(n));
        }

        public async Task<string> PinContentInNodeAsync(string hash, string? nodeId)
        {
            // Try to select an healthy node that doesn't already own the pin, if not specified.
            nodeId ??= (await beeNodeLiveManager.TrySelectHealthyNodeAsync(
                BeeNodeSelectionMode.RoundRobin,
                PinNewContentSelectionContext,
                async node => !await node.IsPinningResourceAsync(hash)))?.Id;
            var newNodeIsFound = nodeId is not null;

            // If isn't available any new node, try to find any healthy node.
            nodeId ??= (await beeNodeLiveManager.TrySelectHealthyNodeAsync(
                BeeNodeSelectionMode.RoundRobin,
                PinNewContentSelectionContext))?.Id;

            if (nodeId is null)
                throw new InvalidOperationException("No healthy nodes available to pin");

            // Schedule task if needed.
            if (newNodeIsFound)
                backgroundJobClient.Enqueue<IPinContentInNodeTask>(
                    task => task.RunAsync(hash, nodeId));

            return nodeId;
        }
    }
}

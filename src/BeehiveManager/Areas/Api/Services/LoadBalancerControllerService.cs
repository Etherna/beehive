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
using Etherna.BeehiveManager.Services.Domain;
using Etherna.BeehiveManager.Services.Utilities;
using MoreLinq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class LoadBalancerControllerService : ILoadBalancerControllerService
    {
        // Fields.
        private readonly IBeeNodeLiveManager beeNodeLiveManager;
        private readonly IBeeNodeService beeNodeService;
        private readonly IBeehiveDbContext dbContext;

        // Constructor.
        public LoadBalancerControllerService(
            IBeeNodeLiveManager beeNodeLiveManager,
            IBeeNodeService beeNodeService,
            IBeehiveDbContext dbContext)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
            this.beeNodeService = beeNodeService;
            this.dbContext = dbContext;
        }

        // Methods.
        public async Task<BeeNodeDto> FindBeeNodeOwnerOfPostageBatchAsync(string batchId)
        {
            var beeNodeLiveInstance = beeNodeLiveManager.GetBeeNodeLiveInstanceByOwnedPostageBatch(batchId);
            var beeNode = await dbContext.BeeNodes.FindOneAsync(beeNodeLiveInstance.Id);

            return new BeeNodeDto(beeNode);
        }

        public async Task<BeeNodeDto> SelectDownloadNodeAsync(string hash)
        {
            // Try to find a pinning node.
            var beeNodeLiveInstances = beeNodeLiveManager.GetBeeNodeLiveInstancesByPinnedContent(hash, true);
            if (beeNodeLiveInstances.Any())
            {
                //select a random one
                var instance = beeNodeLiveInstances.RandomSubset(1).First();
                var node = await dbContext.BeeNodes.FindOneAsync(instance.Id);
                return new BeeNodeDto(node);
            }

            // If there isn't any pinning node, select an alive node.
            beeNodeLiveInstances = beeNodeLiveManager.HealthyNodes;
            if (beeNodeLiveInstances.Any())
            {
                //select a random one
                var instance = beeNodeLiveInstances.RandomSubset(1).First();
                var node = await dbContext.BeeNodes.FindOneAsync(instance.Id);
                return new BeeNodeDto(node);
            }

            // Throw exception because doesn't exist any available node.
            throw new InvalidOperationException("Can't select a valid node");
        }

        public async Task<BeeNodeDto> SelectHealthyNodeAsync()
        {
            // Get random node.
            var selectedNode = await beeNodeService.SelectRandomHealthyNodeAsync();

            return new BeeNodeDto(selectedNode);
        }
    }
}

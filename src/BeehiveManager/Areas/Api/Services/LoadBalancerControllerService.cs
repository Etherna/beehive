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

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
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class PostageControllerService : IPostageControllerService
    {
        // Fields.
        private readonly IBeehiveDbContext beehiveDbContext;
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        // Constructor.
        public PostageControllerService(
            IBeehiveDbContext beehiveDbContext,
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.beehiveDbContext = beehiveDbContext;
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        // Methods.
        public async Task<PostageBatchRefDto> BuyPostageBatchAsync(
            long amount, int depth, long? gasPrice, bool immutable, string? label, string? nodeId)
        {
            // Try to select an healthy node.
            var beeNodeInstance = nodeId is null ?
                beeNodeLiveManager.TrySelectHealthyNode(BeeNodeSelectionMode.RoundRobin) :
                await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);

            if (beeNodeInstance is null)
                throw new InvalidOperationException("No healthy nodes available");

            // Buy postage.
            var batchId = await beeNodeInstance.BuyPostageBatchAsync(amount, depth, label, immutable, gasPrice);

            return new PostageBatchRefDto(batchId, beeNodeInstance.Id);
        }

        public async Task<BeeNodeDto> FindBeeNodeOwnerOfPostageBatchAsync(string batchId)
        {
            var beeNodeLiveInstance = beeNodeLiveManager.AllNodes.FirstOrDefault(n => n.Status.PostageBatchesId?.Contains(batchId) ?? false);
            if (beeNodeLiveInstance is null)
                throw new KeyNotFoundException();

            var beeNode = await beehiveDbContext.BeeNodes.FindOneAsync(beeNodeLiveInstance.Id);

            return new BeeNodeDto(beeNode);
        }
    }
}

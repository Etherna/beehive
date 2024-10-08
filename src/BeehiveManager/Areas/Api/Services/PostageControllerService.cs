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
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using Etherna.BeeNet.Models;
using System;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class PostageControllerService : IPostageControllerService
    {
        // Fields.
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        // Constructor.
        public PostageControllerService(
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        // Methods.
        public async Task<PostageBatchRefDto> BuyPostageBatchAsync(
            BzzBalance amount,
            int depth,
            bool immutable,
            string? label,
            string? nodeId)
        {
            // Select node.
            BeeNodeLiveInstance? beeNodeInstance = null;

            //if is passed a specific node id, use it to select node
            if (nodeId is not null)
            {
                beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);
                if (!beeNodeInstance.IsBatchCreationEnabled)
                    throw new InvalidOperationException("Selected node is not enabled for batch creation");
            }

            //if still null, try to select a random healthy node
            beeNodeInstance ??= await beeNodeLiveManager.TrySelectHealthyNodeAsync(
                BeeNodeSelectionMode.RoundRobin,
                "buyPostageBatch",
                node => Task.FromResult(node.IsBatchCreationEnabled));

            if (beeNodeInstance is null)
                throw new InvalidOperationException("No healthy nodes available for batch creation");

            // Buy postage.
            var batchId = await beeNodeInstance.BuyPostageBatchAsync(amount, depth, label, immutable);

            return new PostageBatchRefDto(batchId, beeNodeInstance.Id);
        }

        public async Task<PostageBatchId> DilutePostageBatchAsync(PostageBatchId batchId, int depth)
        {
            var beeNodeLiveInstance = beeNodeLiveManager.GetBeeNodeLiveInstanceByOwnedPostageBatch(batchId);

            // Top up.
            return await beeNodeLiveInstance.DilutePostageBatchAsync(batchId, depth);
        }

        public async Task<PostageBatchId> TopUpPostageBatchAsync(PostageBatchId batchId, BzzBalance amount)
        {
            var beeNodeLiveInstance = beeNodeLiveManager.GetBeeNodeLiveInstanceByOwnedPostageBatch(batchId);

            // Top up.
            return await beeNodeLiveInstance.TopUpPostageBatchAsync(batchId, amount);
        }
    }
}

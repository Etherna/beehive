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
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.BeeNet.Models;
using System;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Services
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
            var beeNodeLiveInstance = beeNodeLiveManager.SelectUploadNode(batchId);

            // Top up.
            return await beeNodeLiveInstance.DilutePostageBatchAsync(batchId, depth);
        }

        public async Task<PostageBatchId> TopUpPostageBatchAsync(PostageBatchId batchId, BzzBalance amount)
        {
            var beeNodeLiveInstance = beeNodeLiveManager.SelectUploadNode(batchId);

            // Top up.
            return await beeNodeLiveInstance.TopUpPostageBatchAsync(batchId, amount);
        }
    }
}

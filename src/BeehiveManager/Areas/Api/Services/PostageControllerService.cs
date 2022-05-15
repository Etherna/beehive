using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using Etherna.BeeNet.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
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
            long amount, int depth, long? gasPrice, bool immutable, string? label, string? nodeId)
        {
            // Try to select an healthy node.
            var beeNodeInstance = nodeId is null ?
                beeNodeLiveManager.TrySelectHealthyNodeAsync(BeeNodeSelectionMode.RoundRobin) :
                await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);

            if (beeNodeInstance is null)
                throw new InvalidOperationException("No healthy nodes available");

            // Buy postage.
            var batchId = await beeNodeInstance.BuyPostageBatchAsync(amount, depth, label, immutable, gasPrice);

            return new PostageBatchRefDto(batchId, beeNodeInstance.Id);
        }

        public async Task<IEnumerable<PostageBatchDto>> GetPostageBatchesFromAllNodes()
        {
            var batches = new List<PostageBatchDto>();
            foreach (var nodeStatus in beeNodeLiveManager.HealthyNodes)
            {
                try
                {
                    batches.AddRange((await nodeStatus.Client.DebugClient!.GetAllValidPostageBatchesFromAllNodesAsync())
                        .Select(b => new PostageBatchDto(b)));
                }
                catch (Exception e) when (e is BeeNetDebugApiException) { }
            }

            return batches;
        }
    }
}

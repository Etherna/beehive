using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Services.Utilities;
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
        private readonly IBeeNodeClientsManager beeNodeClientsManager;

        // Constructor.
        public PostageControllerService(
            IBeeNodeClientsManager beeNodeClientsManager)
        {
            this.beeNodeClientsManager = beeNodeClientsManager;
        }

        // Methods.
        public async Task<string> BuyPostageBatchAsync(
            int depth, int plurAmount, int? gasPrice, bool immutable, string? label, string? nodeId)
        {
            // Try to select an healthy node.
            var beeNodeClient = nodeId is null ?
                beeNodeClientsManager.TrySelectHealthyNodeClientAsync(BeeNodeSelectionMode.RoundRobin) :
                await beeNodeClientsManager.GetBeeNodeClientAsync(nodeId);

            if (beeNodeClient is null)
                throw new InvalidOperationException("No healthy nodes available");

            // Buy postage.
            return await beeNodeClient.DebugClient!.BuyPostageBatchAsync(plurAmount, depth, label, immutable, gasPrice);
        }

        public async Task<IEnumerable<PostageBatchDto>> GetPostageBatchesFromAllNodes()
        {
            var batches = new List<PostageBatchDto>();
            foreach (var client in beeNodeClientsManager.HealthyClients)
            {
                try
                {
                    batches.AddRange((await client.DebugClient!.GetAllPostageBatchesAsync()).Select(b => new PostageBatchDto(b)));
                }
                catch (Exception e) when (e is BeeNetDebugApiException) { }
            }

            return batches;
        }
    }
}

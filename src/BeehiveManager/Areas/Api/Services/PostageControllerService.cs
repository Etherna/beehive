using Etherna.BeehiveManager.Services.Utilities;
using System;
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
            var batchDto = await beeNodeClient.DebugClient!.BuyPostageBatchAsync(plurAmount, depth, label, immutable, gasPrice);

            return batchDto.BatchId;
        }
    }
}

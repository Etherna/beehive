using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.BeehiveManager.Services.Utilities;
using System;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class RetrieveBeeNodeAddressesTask : IRetrieveBeeNodeAddressesTask
    {
        // Consts.
        public const string TaskId = "retrieveBeeNodeAddressesTask";

        // Fields.
        private readonly IBeeNodesManager beeNodesManager;
        private readonly IBeehiveContext context;

        // Constructors.
        public RetrieveBeeNodeAddressesTask(
            IBeeNodesManager beeNodesManager,
            IBeehiveContext context)
        {
            this.beeNodesManager = beeNodesManager;
            this.context = context;
        }

        // Methods.
        public async Task RunAsync(string nodeId)
        {
            // Get client.
            var node = await context.BeeNodes.FindOneAsync(nodeId);

            // Verify conditions.
            if (node.Addresses is not null)
                return;
            if (node.DebugPort is null)
                throw new InvalidOperationException("Node is not configured for debug api");

            // Get info.
            var nodeClient = beeNodesManager.GetBeeNodeClient(node);
            var response = await nodeClient.DebugClient!.AddressesAsync();

            // Update node.
            node.SetAddresses(new BeeNodeAddresses(
                response.Ethereum,
                response.Overlay,
                response.Pss_public_key,
                response.Public_key));

            // Save changes.
            await context.SaveChangesAsync();
        }
    }
}

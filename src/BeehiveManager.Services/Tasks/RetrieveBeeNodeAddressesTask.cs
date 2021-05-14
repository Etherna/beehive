using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Clients.DebugApi;
using System.Net.Http;
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
            var node = await context.BeeNodes.TryFindOneAsync(nodeId);

            // Verify conditions.
            if (node is null)
                return; //can't find the node in db
            if (node.Addresses is not null)
                return; //don't need any operation
            if (node.DebugPort is null)
                return; //node is not configured for use debug api

            // Get info.
            var nodeClient = beeNodesManager.GetBeeNodeClient(node);
            Response response;
            try { response = await nodeClient.DebugClient!.AddressesAsync(); }
            catch (BeeNetDebugApiException) { return; } //issues contacting the node instance api
            catch (HttpRequestException) { return; }

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

using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Clients.DebugApi;
using Hangfire;
using MongoDB.Driver;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class RefreshClusterNodesStatusTask : IRefreshClusterNodesStatusTask
    {
        // Consts.
        public const string TaskId = "refreshClusterNodesStatusTask";

        // Fields.
        private readonly IBackgroundJobClient backgroundJobClient;
        private readonly IBeeNodesManager beeNodesManager;
        private readonly IBeehiveContext context;

        // Constructors.
        public RefreshClusterNodesStatusTask(
            IBackgroundJobClient backgroundJobClient,
            IBeeNodesManager beeNodesManager,
            IBeehiveContext context)
        {
            this.backgroundJobClient = backgroundJobClient;
            this.beeNodesManager = beeNodesManager;
            this.context = context;
        }

        // Methods.
        public async Task RunAsync()
        {
            // List all nodes.
            await context.BeeNodes.Collection.Find(FilterDefinition<BeeNode>.Empty, new FindOptions { NoCursorTimeout = true })
                .ForEachAsync(async node =>
                {
                    // Verify if has addresses.
                    if (node.Addresses is null)
                        backgroundJobClient.Enqueue<IRetrieveBeeNodeAddressesTask>(task => task.RunAsync(node.Id));

                    // Get info.
                    var nodeClient = beeNodesManager.GetBeeNodeClient(node);
                    if (nodeClient.DebugClient is null) //skip if doesn't have a debug api config
                        return;

                    long totalUncashed = 0;
                    try
                    {
                        var peersResponse = await nodeClient.DebugClient.ChequebookChequeGetAsync();
                        var peers = peersResponse.Lastcheques.Select(c => c.Peer);

                        foreach (var peer in peers)
                        {
                            var amountResponse = await nodeClient.DebugClient.ChequebookCashoutGetAsync(peer);
                            totalUncashed += amountResponse.CumulativePayout;
                        }

                    }
                    catch (BeeNetDebugApiException) { return; } //issues contacting the node instance api
                    catch (HttpRequestException) { return; }

                    // Update node.
                    node.Status = new BeeNodeStatus(totalUncashed);

                    // Save changes.
                    await context.SaveChangesAsync();
                });
        }
    }
}

using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.BeeNet;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.BeeNet.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class BeeNodeLiveInstance
    {
        // Fields.
        private readonly IBeehiveDbContext beehiveDbContext;

        // Constructor.
        internal BeeNodeLiveInstance(
            BeeNode beeNode,
            IBeehiveDbContext beehiveDbContext)
        {
            Id = beeNode.Id;
            Client = new BeeNodeClient(beeNode.Url.AbsoluteUri, beeNode.GatewayPort, beeNode.DebugPort);
            Status = new BeeNodeStatus();
            this.beehiveDbContext = beehiveDbContext;
        }

        // Properties.
        public string Id { get; }
        public BeeNodeClient Client { get; }
        public BeeNodeStatus Status { get; }

        // Public methods.
        public Task<string> BuyPostageBatchAsync(long amount, int depth, string? label, bool immutable, long? gasPrice) =>
            Client.DebugClient!.BuyPostageBatchAsync(amount, depth, label, immutable, gasPrice);

        public async Task<bool> TryRefreshStatusAsync(bool forceFullRefresh = false)
        {
            var errors = new List<string>();

            // Check if node is alive.
            try
            {
                var result = await Client.DebugClient!.GetReadinessAsync();
                Status.IsAlive = result.Status == "ok";

                // Verify and update api version.
                if (Status.IsAlive)
                {
                    /* If the version is not recognized (default case) use the last version available.
                     * This because is more probable that the actual version is more advanced than the recognized one,
                     * and so APIs are more similar to the last version than the older versions.
                     */
                    var currentGatewayApiVersion = result.ApiVersion switch
                    {
                        "2.0.0" => GatewayApiVersion.v2_0_0,
                        "3.0.0" => GatewayApiVersion.v3_0_0,
                        _ => GatewayApiVersion.v3_0_0
                    };
                    var currentDebugApiVersion = result.DebugApiVersion switch
                    {
                        "1.2.0" => DebugApiVersion.v1_2_0,
                        "1.2.1" => DebugApiVersion.v1_2_1,
                        "2.0.0" => DebugApiVersion.v2_0_0,
                        _ => DebugApiVersion.v2_0_0
                    };

                    if (Client.GatewayClient!.CurrentApiVersion != currentGatewayApiVersion)
                        Client.GatewayClient.CurrentApiVersion = currentGatewayApiVersion;
                    if (Client.DebugClient!.CurrentApiVersion != currentDebugApiVersion)
                        Client.DebugClient.CurrentApiVersion = currentDebugApiVersion;
                }
            }
            catch (Exception e) when (
                e is BeeNetDebugApiException ||
                e is HttpRequestException ||
                e is SocketException)
            {
                errors.Add("Node is not alive, or API are not recognized");
                Status.IsAlive = false;

                return false;
            }

#pragma warning disable CA1031 // Do not catch general exception types
            // Initialize if necessary (or if forced).
            if (!Status.IsInitialized || forceFullRefresh)
            {
                //bee node domain model
                try { await InitializeBeeDomainModelAsync(); }
                catch { errors.Add("Can't initialize node on db"); }

                //postage batches
                try { await InitializePostageBatchesAsync(); }
                catch { errors.Add("Can't initialize postage batches"); }

                Status.IsInitialized = !errors.Any();
            }
            Status.Errors = errors;
#pragma warning restore CA1031 // Do not catch general exception types

            return true;
        }

        // Helpers.
        private async Task InitializeBeeDomainModelAsync()
        {
            var node = await beehiveDbContext.BeeNodes.FindOneAsync(Id);
            if (node is not null && node.Addresses is null)
            {
                var response = await Client.DebugClient!.GetAddressesAsync();

                // Update node.
                node.SetAddresses(new BeeNodeAddresses(
                    response.Ethereum,
                    response.Overlay,
                    response.PssPublicKey,
                    response.PublicKey));

                // Save changes.
                await beehiveDbContext.SaveChangesAsync();
            }
        }

        private async Task InitializePostageBatchesAsync()
        {
            var batches = await Client.DebugClient!.GetOwnedPostageBatchesByNodeAsync();
            Status.PostageBatches = batches.Select(b => new PostageBatch(b));
        }
    }
}

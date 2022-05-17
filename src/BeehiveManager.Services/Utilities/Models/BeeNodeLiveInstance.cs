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

using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.BeeNet;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.BeeNet.Exceptions;
using Etherna.ExecContext.AsyncLocal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class BeeNodeLiveInstance
    {
        // Fields.
        private readonly IBeehiveDbContext beehiveDbContext;
        private readonly SemaphoreSlim statusRefreshSemaphore = new(1, 1);

        // Constructor.
        internal BeeNodeLiveInstance(
            BeeNode beeNode,
            IBeehiveDbContext beehiveDbContext)
        {
            Id = beeNode.Id;
            Client = new BeeNodeClient(beeNode.BaseUrl.AbsoluteUri, beeNode.GatewayPort, beeNode.DebugPort);
            RequireFullStatusRefresh = true;
            Status = new BeeNodeStatus();
            this.beehiveDbContext = beehiveDbContext;
        }

        // Properties.
        public string Id { get; }
        public BeeNodeClient Client { get; }
        public bool DomainModelIsInitialized { get; private set; }
        public bool RequireFullStatusRefresh { get; private set; }
        public BeeNodeStatus Status { get; private set; }

        // Public methods.
        public async Task<string> BuyPostageBatchAsync(long amount, int depth, string? label, bool immutable, long? gasPrice)
        {
            var batchId = await Client.DebugClient!.BuyPostageBatchAsync(amount, depth, label, immutable, gasPrice);

            //add batchId with full status refresh
            RequireFullStatusRefresh = true;

            return batchId;
        }

        /// <summary>
        /// Try to refresh node live status
        /// </summary>
        /// <param name="forceFullRefresh">True if status have to be full checked</param>
        /// <returns>True if node was alive</returns>
        public async Task<bool> TryRefreshStatusAsync(bool forceFullRefresh = false)
        {
            await statusRefreshSemaphore.WaitAsync();

            try
            {
                // Check if node is alive.
                try
                {
                    var result = await Client.DebugClient!.GetReadinessAsync();
                    var isAlive = result.Status == "ok";

                    if (!isAlive)
                    {
                        Status = new BeeNodeStatus
                        {
                            Errors = new[] { "Node is not ready" },
                            IsAlive = false,
                            PostageBatchesId = Status.PostageBatchesId
                        };
                        return false;
                    }

                    /* Verify and update api version.
                     * 
                     * If the version is not recognized (switch default case) use the last version available.
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
                catch (Exception e) when (
                    e is BeeNetDebugApiException ||
                    e is HttpRequestException ||
                    e is SocketException)
                {
                    Status = new BeeNodeStatus
                    {
                        Errors = new[] { "Exception invoking node API" },
                        IsAlive = false,
                        PostageBatchesId = Status.PostageBatchesId
                    };
                    return false;
                }

                /***
                 * If here, node is Alive
                 */
#pragma warning disable CA1031 // Do not catch general exception types

                // Verify domain model initialization.
                if (!DomainModelIsInitialized)
                {
                    try
                    {
                        await InitializeBeeDomainModelAsync();
                        DomainModelIsInitialized = true;
                    }
                    catch { }
                }

                // Full refresh if is required (or if is forced).
                var errors = new List<string>();
                var postageBatchesId = Status.PostageBatchesId;

                if (RequireFullStatusRefresh || forceFullRefresh)
                {
                    //postage batches
                    try
                    {
                        var batches = await Client.DebugClient!.GetOwnedPostageBatchesByNodeAsync();
                        postageBatchesId = batches.Select(b => b.Id);
                    }
                    catch { errors.Add("Can't initialize postage batches"); }
                }

#pragma warning restore CA1031 // Do not catch general exception types

                Status = new BeeNodeStatus
                {
                    Errors = errors,
                    IsAlive = true,
                    PostageBatchesId = postageBatchesId
                };
                RequireFullStatusRefresh &= errors.Any();

                return true;
            }
            finally
            {
                statusRefreshSemaphore.Release();
            }
        }

        // Helpers.
        private async Task InitializeBeeDomainModelAsync()
        {
            using var execContext = AsyncLocalContext.Instance.InitAsyncLocalContext();

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
    }
}

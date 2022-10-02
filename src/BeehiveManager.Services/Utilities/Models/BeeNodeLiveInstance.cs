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

using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeeNet;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.BeeNet.Exceptions;
using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<string, bool> _inProgressPins = new(); //content hash -> (irrelevant). Needed for concurrency
        private readonly SemaphoreSlim statusRefreshSemaphore = new(1, 1);

        // Constructor.
        internal BeeNodeLiveInstance(
            BeeNode beeNode)
        {
            Id = beeNode.Id;
            Client = new BeeNodeClient(beeNode.BaseUrl.AbsoluteUri, beeNode.GatewayPort, beeNode.DebugPort);
            RequireFullStatusRefresh = true;
            Status = new BeeNodeStatus();
        }

        // Properties.
        public string Id { get; }
        public BeeNodeClient Client { get; }
        public IEnumerable<string> InProgressPins => _inProgressPins.Keys;
        public bool RequireFullStatusRefresh { get; private set; }
        public BeeNodeStatus Status { get; private set; }

        // Public methods.
        public async Task<string> BuyPostageBatchAsync(long amount, int depth, string? label, bool immutable, long? gasPrice)
        {
            var batchId = await Client.DebugClient!.BuyPostageBatchAsync(amount, depth, label, immutable, gasPrice);

            //immediately add the batch to the node
            await statusRefreshSemaphore.WaitAsync();
            try
            {
                Status = new BeeNodeStatus(
                    Status.Addresses,
                    Status.Errors,
                    Status.HeartbeatTimeStamp,
                    Status.IsAlive,
                    Status.PinnedHashes,
                    (Status.PostageBatchesId ?? Array.Empty<string>()).Append(batchId).ToHashSet());
            }
            finally
            {
                statusRefreshSemaphore.Release();
            }

            return batchId;
        }

        public Task<string> DilutePostageBatchAsync(string batchId, int depth) =>
            Client.DebugClient!.DilutePostageBatchAsync(batchId, depth);

        public async Task<bool> IsPinningResourceAsync(string hash)
        {
            try
            {
                await Client.GatewayClient!.GetPinStatusAsync(hash);
                return true;
            }
            catch (BeeNetGatewayApiException e) when (e.StatusCode == 404)
            {
                return false;
            }
        }

        public async Task NotifyPinnedResourceAsync(string hash)
        {
            //immediately add the pin to the node
            await statusRefreshSemaphore.WaitAsync();
            try
            {
                Status = new BeeNodeStatus(
                    Status.Addresses,
                    Status.Errors,
                    Status.HeartbeatTimeStamp,
                    Status.IsAlive,
                    (Status.PinnedHashes ?? Array.Empty<string>()).Append(hash).ToHashSet(),
                    Status.PostageBatchesId);
            }
            finally
            {
                statusRefreshSemaphore.Release();
            }
        }

        public async Task PinResourceAsync(string hash)
        {
            _inProgressPins.TryAdd(hash, false);

            try
            {
                await Client.GatewayClient!.CreatePinAsync(hash);

                //add pinned hash with full status refresh
                RequireFullStatusRefresh = true;
            }
            finally
            {
                _inProgressPins.TryRemove(hash, out _);
            }
        }

        public async Task RemovePinnedResourceAsync(string hash)
        {
            try
            {
                await Client.GatewayClient!.DeletePinAsync(hash);

                //remove pinned hash with full status refresh
                RequireFullStatusRefresh = true;
            }
            catch (BeeNetGatewayApiException e) when(e.StatusCode == 404)
            {
                throw new KeyNotFoundException();
            }
        }

        public Task<string> TopUpPostageBatchAsync(string batchId, long amount) =>
            Client.DebugClient!.TopUpPostageBatchAsync(batchId, amount);

        /// <summary>
        /// Try to refresh node live status
        /// </summary>
        /// <param name="forceFullRefresh">True if status have to be full checked</param>
        /// <returns>True if node was alive</returns>
        public async Task<bool> TryRefreshStatusAsync(bool forceFullRefresh = false)
        {
            await statusRefreshSemaphore.WaitAsync();

            var heartbeatTimeStamp = DateTime.UtcNow;

            try
            {
                // Check if node is alive.
                try
                {
                    var result = await Client.DebugClient!.GetReadinessAsync();
                    var isAlive = result.Status == "ok";

                    if (!isAlive)
                    {
                        Status = new BeeNodeStatus(
                            Status.Addresses,
                            new[] { "Node is not ready" },
                            heartbeatTimeStamp,
                            false,
                            Status.PinnedHashes,
                            Status.PostageBatchesId);
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
                        "3.0.2" => GatewayApiVersion.v3_0_2,
                        _ => Enum.GetValues<GatewayApiVersion>().OrderByDescending(e => e.ToString()).First()
                    };
                    var currentDebugApiVersion = result.DebugApiVersion switch
                    {
                        "3.0.2" => DebugApiVersion.v3_0_2,
                        _ => Enum.GetValues<DebugApiVersion>().OrderByDescending(e => e.ToString()).First()
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
                    Status = new BeeNodeStatus(
                        Status.Addresses,
                        new[] { "Exception invoking node API" },
                        heartbeatTimeStamp,
                        false,
                        Status.PinnedHashes,
                        Status.PostageBatchesId
                    );
                    return false;
                }

#pragma warning disable CA1031 // Do not catch general exception types

                /***
                 * If here, node is Alive
                 ***/

                // Verify addresses initialization.
                var addresses = Status.Addresses;
                if (addresses is null)
                {
                    try
                    {
                        var response = await Client.DebugClient!.GetAddressesAsync();
                        addresses = new BeeNodeAddresses(
                            response.Ethereum,
                            response.Overlay,
                            response.PssPublicKey,
                            response.PublicKey);
                    }
                    catch { }
                }

                // Full refresh if is required (or if is forced).
                var errors = new List<string>();
                var pinnedHashes = Status.PinnedHashes;
                var postageBatchesId = Status.PostageBatchesId;

                if (RequireFullStatusRefresh || forceFullRefresh)
                {
                    //pinned hashes
                    try
                    {
                        pinnedHashes = await Client.GatewayClient!.GetAllPinsAsync();
                    }
                    catch { errors.Add("Can't read pinned hashes"); }

                    //postage batches
                    try
                    {
                        /* Union is required, because postage batches just created could not appear from the node request.
                         * Because of this, if we added a new created postage, and we try to refresh with only info from node,
                         * the postage Id reference could be lost.
                         * Unione instead never remove a postage batch id. This is fine, because an owned postage batch can't be removed
                         * by node's logic. It only can expire, but this is not concern of this part of code.
                         */
                        var batches = await Client.DebugClient!.GetOwnedPostageBatchesByNodeAsync();
                        postageBatchesId = (postageBatchesId ?? Array.Empty<string>()).Union(batches.Select(b => b.Id)).ToArray();
                    }
                    catch { errors.Add("Can't read postage batches"); }
                }

#pragma warning restore CA1031 // Do not catch general exception types

                Status = new BeeNodeStatus(
                    addresses,
                    errors,
                    heartbeatTimeStamp,
                    true,
                    pinnedHashes,
                    postageBatchesId
                );
                RequireFullStatusRefresh &= errors.Any();

                return true;
            }
            finally
            {
                statusRefreshSemaphore.Release();
            }
        }
    }
}

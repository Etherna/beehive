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
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class BeeNodeLiveInstance
    {
        // Fields.
        private readonly ConcurrentDictionary<string, bool> _inProgressPins = new(); //content hash -> (irrelevant). Needed for concurrency

        // Constructor.
        internal BeeNodeLiveInstance(
            BeeNode beeNode)
        {
            Id = beeNode.Id;
            Client = new BeeNodeClient(beeNode.BaseUrl.AbsoluteUri, beeNode.GatewayPort, beeNode.DebugPort);
            IsBatchCreationEnabled = beeNode.IsBatchCreationEnabled;
            Status = new BeeNodeStatus();
        }

        // Properties.
        public string Id { get; }
        public BeeNodeClient Client { get; }
        public IEnumerable<string> InProgressPins => _inProgressPins.Keys;
        public bool IsBatchCreationEnabled { get; set; }
        public BeeNodeStatus Status { get; }

        // Public methods.
        public async Task<string> BuyPostageBatchAsync(long amount, int depth, string? label, bool immutable, long? gasPrice)
        {
            var batchId = await Client.DebugClient!.BuyPostageBatchAsync(amount, depth, label, immutable, gasPrice);

            //immediately add the batch to the node status
            Status.AddPostageBatchId(batchId);

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

        public void NotifyPinnedResource(string hash)
        {
            //immediately add the pin to the node status
            Status.AddPinnedHash(hash);
        }

        public async Task PinResourceAsync(string hash)
        {
            _inProgressPins.TryAdd(hash, false);

            try
            {
                await Client.GatewayClient!.CreatePinAsync(hash);
                Status.AddPinnedHash(hash);
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
                Status.RemovePinnedHash(hash);
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
            var heartbeatTimeStamp = DateTime.UtcNow;

            // Verify node health and readiness.
            try
            {
                //health
                var healthResult = await Client.DebugClient!.GetHealthAsync();

                if (healthResult.Status != BeeNet.DtoModels.StatusEnumDto.Ok)
                {
                    Status.FailedHeartbeatAttempt(
                        new[] { "Node is not healthy" },
                        heartbeatTimeStamp);
                    return false;
                }

                /* Verify and update api version.
                 * 
                 * If the version is not recognized (switch default case) use the last version available.
                 * This because is more probable that the actual version is more advanced than the recognized one,
                 * and so APIs are more similar to the last version than the older versions.
                 */
                var currentGatewayApiVersion = healthResult.ApiVersion switch
                {
                    "4.0.0" => GatewayApiVersion.v4_0_0,
                    _ => Enum.GetValues<GatewayApiVersion>().OrderByDescending(e => e.ToString()).First()
                };
                var currentDebugApiVersion = healthResult.DebugApiVersion switch
                {
                    "4.0.0" => DebugApiVersion.v4_0_0,
                    _ => Enum.GetValues<DebugApiVersion>().OrderByDescending(e => e.ToString()).First()
                };

                if (Client.GatewayClient!.CurrentApiVersion != currentGatewayApiVersion)
                    Client.GatewayClient.CurrentApiVersion = currentGatewayApiVersion;
                if (Client.DebugClient!.CurrentApiVersion != currentDebugApiVersion)
                    Client.DebugClient.CurrentApiVersion = currentDebugApiVersion;

                //readiness
                var isReady = await Client.DebugClient!.GetReadinessAsync();

                if (!isReady)
                {
                    Status.FailedHeartbeatAttempt(
                        new[] { "Node is not ready" },
                        heartbeatTimeStamp);
                    return false;
                }
            }
            catch (Exception e) when (
                e is BeeNetDebugApiException ||
                e is HttpRequestException ||
                e is SocketException)
            {
                Status.FailedHeartbeatAttempt(
                    new[] { "Exception invoking node API" },
                    heartbeatTimeStamp);
                return false;
            }

#pragma warning disable CA1031 // Do not catch general exception types

            /***
             * If here, node is Alive
             ***/

            // Verify addresses initialization.
            if (Status.Addresses is null)
            {
                try
                {
                    var response = await Client.DebugClient!.GetAddressesAsync();
                    Status.InitializeAddresses(new BeeNodeAddresses(
                        response.Ethereum,
                        response.Overlay,
                        response.PssPublicKey,
                        response.PublicKey));
                }
                catch { }
            }

            // Full refresh if is required (or if is forced).
            var errors = new List<string>();
            IEnumerable<string>? refreshedPinnedHashes = null;
            IEnumerable<string>? refreshedPostageBatchesId = null;

            if (Status.RequireFullRefresh || forceFullRefresh)
            {
                //pinned hashes
                try
                {
                    refreshedPinnedHashes = await Client.GatewayClient!.GetAllPinsAsync();
                }
                catch { errors.Add("Can't read pinned hashes"); }

                //postage batches
                try
                {
                    var batches = await Client.DebugClient!.GetOwnedPostageBatchesByNodeAsync();
                    refreshedPostageBatchesId = batches.Select(b => b.Id);
                }
                catch { errors.Add("Can't read postage batches"); }
            }

#pragma warning restore CA1031 // Do not catch general exception types

            Status.SucceededHeartbeatAttempt(
                errors,
                heartbeatTimeStamp,
                refreshedPinnedHashes,
                refreshedPostageBatchesId);

            return true;
        }
    }
}

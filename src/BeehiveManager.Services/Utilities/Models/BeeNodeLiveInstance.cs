// Copyright 2021-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeeNet;
using Etherna.BeeNet.Clients;
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
            Client = new BeeClient(beeNode.BaseUrl.AbsoluteUri, beeNode.GatewayPort);
            IsBatchCreationEnabled = beeNode.IsBatchCreationEnabled;
            Status = new BeeNodeStatus();
        }

        // Properties.
        public string Id { get; }
        public BeeClient Client { get; }
        public IEnumerable<string> InProgressPins => _inProgressPins.Keys;
        public bool IsBatchCreationEnabled { get; set; }
        public BeeNodeStatus Status { get; }

        // Public methods.
        public async Task<string> BuyPostageBatchAsync(long amount, int depth, string? label, bool immutable)
        {
            var batchId = await Client.BuyPostageBatchAsync(amount, depth, label, immutable);

            //immediately add the batch to the node status
            Status.AddPostageBatchId(batchId);

            return batchId;
        }

        public Task<string> DilutePostageBatchAsync(string batchId, int depth) =>
            Client.DilutePostageBatchAsync(batchId, depth);

        public async Task<bool> IsPinningResourceAsync(string hash)
        {
            try
            {
                await Client.GetPinStatusAsync(hash);
                return true;
            }
            catch (BeeNetApiException e) when (e.StatusCode == 404)
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
                await Client.CreatePinAsync(hash);
                Status.AddPinnedHash(hash);
            }
            catch (BeeNetApiException e) when (e.StatusCode == 404)
            {
                throw new KeyNotFoundException();
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
                await Client.DeletePinAsync(hash);
                Status.RemovePinnedHash(hash);
            }
            catch (BeeNetApiException e) when(e.StatusCode == 404)
            {
                throw new KeyNotFoundException();
            }
        }

        public Task<string> TopUpPostageBatchAsync(string batchId, long amount) =>
            Client.TopUpPostageBatchAsync(batchId, amount);

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
                var healthResult = await Client.GetHealthAsync();

                if (healthResult.Status != BeeNet.Models.StatusValues.Ok)
                {
                    Status.FailedHeartbeatAttempt(
                        new[] { "Node is not healthy" },
                        heartbeatTimeStamp);
                    return false;
                }

                //readiness
                var isReady = await Client.GetReadinessAsync();

                if (!isReady)
                {
                    Status.FailedHeartbeatAttempt(
                        new[] { "Node is not ready" },
                        heartbeatTimeStamp);
                    return false;
                }
            }
            catch (Exception e) when (
                e is BeeNetApiException ||
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
                    var response = await Client.GetAddressesAsync();
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
                    refreshedPinnedHashes = await Client.GetAllPinsAsync();
                }
                catch { errors.Add("Can't read pinned hashes"); }

                //postage batches
                try
                {
                    var batches = await Client.GetOwnedPostageBatchesByNodeAsync();
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

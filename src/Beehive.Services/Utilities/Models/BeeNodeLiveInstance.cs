// Copyright 2021-present Etherna SA
// This file is part of Beehive.
// 
// Beehive is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Beehive is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Beehive.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Beehive.Domain.Models;
using Etherna.BeeNet;
using Etherna.BeeNet.Exceptions;
using Etherna.BeeNet.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Utilities.Models
{
    public class BeeNodeLiveInstance
    {
        // Fields.
        private readonly ConcurrentDictionary<SwarmHash, bool> _inProgressPins = new(); //content hash -> (irrelevant). Needed for concurrency

        // Constructor.
        internal BeeNodeLiveInstance(
            BeeNode beeNode)
        {
            Id = beeNode.Id;
            Client = new BeeClient(beeNode.GatewayUrl);
            IsBatchCreationEnabled = beeNode.IsBatchCreationEnabled;
            Status = new BeeNodeStatus();
        }

        // Properties.
        public string Id { get; }
        public BeeClient Client { get; }
        public IEnumerable<SwarmHash> InProgressPins => _inProgressPins.Keys;
        public bool IsBatchCreationEnabled { get; set; }
        public BeeNodeStatus Status { get; }

        // Public methods.
        public async Task<PostageBatchId> BuyPostageBatchAsync(BzzBalance amount, int depth, string? label, bool immutable)
        {
            var batchId = await Client.BuyPostageBatchAsync(amount, depth, label, immutable);

            //immediately add the batch to the node status
            Status.AddPostageBatchId(batchId);

            return batchId;
        }

        public Task<PostageBatchId> DilutePostageBatchAsync(PostageBatchId batchId, int depth) =>
            Client.DilutePostageBatchAsync(batchId, depth);

        public async Task<bool> IsPinningResourceAsync(SwarmHash hash)
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

        public void NotifyPinnedResource(SwarmHash hash)
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

        public async Task RemovePinnedResourceAsync(SwarmHash hash)
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

        public Task<PostageBatchId> TopUpPostageBatchAsync(PostageBatchId batchId, BzzBalance amount) =>
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

                if (!healthResult.IsStatusOk)
                {
                    Status.FailedHeartbeatAttempt(
                        ["Node is not healthy"],
                        heartbeatTimeStamp);
                    return false;
                }

                //readiness
                var isReady = await Client.GetReadinessAsync();

                if (!isReady)
                {
                    Status.FailedHeartbeatAttempt(
                        ["Node is not ready"],
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
                    ["Exception invoking node API"],
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
            IEnumerable<SwarmHash>? refreshedPinnedHashes = null;
            IEnumerable<PostageBatchId>? refreshedPostageBatchesId = null;

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

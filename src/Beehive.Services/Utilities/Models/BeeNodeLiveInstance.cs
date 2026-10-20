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
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities.DevNode;
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Utilities.Models
{
    public class BeeNodeLiveInstance
    {
        // Fields.
        private readonly ILogger logger;

        // Constructors.
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The client lives as long as the node instance")]
        internal BeeNodeLiveInstance(BeeNode beeNode, ILogger logger)
            : this(
                beeNode.Id,
                new SwarmClient(beeNode.ConnectionString, apiCompatibility: SwarmClients.Bee),
                beeNode.IsBatchCreationEnabled,
                logger)
        { }

        internal BeeNodeLiveInstance(DevNodeClient devNodeClient, ILogger logger)
            : this(DevNodeClient.NodeId, devNodeClient, true, logger)
        { }

        internal BeeNodeLiveInstance(
            string id,
            ISwarmClient client,
            bool isBatchCreationEnabled,
            ILogger logger)
        {
            Id = id;
            Client = client;
            ChunkStore = new SwarmClientChunkStore(client);
            IsBatchCreationEnabled = isBatchCreationEnabled;
            this.logger = logger;
            Status = new BeeNodeStatus();
        }

        // Properties.
        public string Id { get; }
        public SwarmClientChunkStore ChunkStore { get; }
        public ISwarmClient Client { get; }
        public bool IsBatchCreationEnabled { get; set; }
        public BeeNodeStatus Status { get; }

        // Public methods.
        public Task<EthTxHash> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            ulong? gasLimit,
            XDaiValue? gasPrice) =>
            Client.DilutePostageBatchAsync(batchId, depth, gasPrice, gasLimit);

        public Task<PostageBatch> GetPostageBatchAsync(PostageBatchId batchId) =>
            Client.GetPostageBatchAsync(batchId);

        public async Task<(IEnumerable<uint> collisions, int depth)> GetPostageBatchBucketsCollisionsAsync(
            PostageBatchId batchId)
        {
            var buckets = await Client.GetPostageBatchBucketsAsync(batchId);
            return (buckets.Collisions, buckets.Depth);
        }

        public Task<EthTxHash> TopUpPostageBatchAsync(
            PostageBatchId batchId,
            BzzValue amount,
            ulong? gasLimit,
            XDaiValue? gasPrice) =>
            Client.TopUpPostageBatchAsync(batchId, amount, gasPrice, gasLimit);

        /// <summary>
        /// Try to refresh node live status
        /// </summary>
        /// <returns>True if node was alive</returns>
        public async Task<bool> TryRefreshStatusAsync()
        {
            var heartbeatTimeStamp = DateTime.UtcNow;

            // The state is logged on the first heartbeat, then only when it changes.
            var isFirstHeartbeat = Status.HeartbeatTimeStamp == default;
            var wasAlive = Status.IsAlive;

            // Verify node health and readiness.
            string? failureReason = null;
            Exception? failureException = null;
            try
            {
                var healthResult = await Client.GetHealthAsync(); //health
                if (!healthResult.IsStatusOk)
                    failureReason = "Node is not healthy";
                else if (!await Client.GetReadinessAsync()) //readiness
                    failureReason = "Node is not ready";
            }
            catch (Exception e) when (
                e is SwarmSdkApiException ||
                e is HttpRequestException ||
                e is SocketException)
            {
                failureReason = "Exception invoking node API";
                failureException = e;
            }

            if (failureReason is not null)
            {
                Status.FailedHeartbeatAttempt([failureReason], heartbeatTimeStamp);
                if (isFirstHeartbeat || wasAlive)
                    logger.BeeNodeNotAlive(Id, failureReason, failureException);
                return false;
            }
            
            /***
             * If here, node is Alive
             ***/

            // Verify addresses initialization.
#pragma warning disable CA1031 // Do not catch general exception types
            if (Status.Addresses is null)
            {
                try
                {
                    var response = await Client.GetNodeAddressesAsync();
                    Status.InitializeAddresses(new BeeNodeAddresses(
                        response.Ethereum,
                        response.Overlay,
                        response.PssPublicKey,
                        response.PublicKey));
                }
                catch (Exception e)
                {
                    logger.BeeNodeAddressesUnavailable(Id, e);
                }
            }
#pragma warning restore CA1031 // Do not catch general exception types

            Status.SucceededHeartbeatAttempt(heartbeatTimeStamp);
            if (isFirstHeartbeat || !wasAlive)
                logger.BeeNodeAlive(Id);

            return true;
        }
    }
}

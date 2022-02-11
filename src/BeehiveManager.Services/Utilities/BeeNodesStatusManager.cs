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
using Etherna.BeehiveManager.Services.Tasks;
using Etherna.BeeNet;
using Etherna.BeeNet.Exceptions;
using Etherna.MongoDB.Driver;
using Hangfire;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Utilities
{
    /// <summary>
    /// Keep singleton instances of BeeNodeClients for performance optimization
    /// </summary>
    class BeeNodesStatusManager : IBeeNodesStatusManager, IDisposable
    {
        // Consts.
        private const int HeartbeatPeriod = 10000; //10s

        // Fields.
        private readonly IBackgroundJobClient backgroundJobClient;
        private readonly IBeehiveDbContext beehiveDbContext;
        private Timer? heartbeatTimer;
        private BeeNodeStatus? lastSelectedNodeRoundRobin;
        private readonly ConcurrentDictionary<string, BeeNodeStatus> beeNodesStatus = new(); //Id -> Status
        private readonly Random rand = new();

        // Constructor and dispose.
        public BeeNodesStatusManager(
            IBackgroundJobClient backgroundJobClient,
            IBeehiveDbContext beehiveDbContext)
        {
            this.backgroundJobClient = backgroundJobClient;
            this.beehiveDbContext = beehiveDbContext;
        }
        public void Dispose()
        {
            heartbeatTimer?.Dispose();
        }

        // Properties.
        public IEnumerable<BeeNodeClient> HealthyClients =>
            beeNodesStatus.Values.Where(status => status.IsAlive)
                                    .Select(s => s.Client);

        // Methods.
        public BeeNodeStatus AddBeeNode(BeeNode beeNode)
        {
            var client = new BeeNodeClient(beeNode.Url.AbsoluteUri, beeNode.GatewayPort, beeNode.DebugPort);
            var status = new BeeNodeStatus(beeNode, client);
            beeNodesStatus.TryAdd(beeNode.Id, status);

            return beeNodesStatus[beeNode.Id];
        }

        public async Task<BeeNodeStatus> GetBeeNodeStatusAsync(string nodeId)
        {
            if (beeNodesStatus.ContainsKey(nodeId))
                return beeNodesStatus[nodeId];

            var beeNode = await beehiveDbContext.BeeNodes.FindOneAsync(nodeId);
            return AddBeeNode(beeNode);
        }

        public async Task LoadAllNodesAsync()
        {
            var nodes = await beehiveDbContext.BeeNodes.QueryElementsAsync(
                elements => elements.ToListAsync());
            foreach (var node in nodes)
                AddBeeNode(node);
        }

        public bool RemoveBeeNode(string nodeId) =>
            beeNodesStatus.TryRemove(nodeId, out _);

        public void StartHealthHeartbeat() =>
            heartbeatTimer = new Timer(async _ => await HeartbeatCallbackAsync(), null, 0, HeartbeatPeriod);

        public void StopHealthHeartbeat() =>
            heartbeatTimer?.Change(Timeout.Infinite, 0);

        public BeeNodeStatus? TrySelectHealthyNodeAsync(BeeNodeSelectionMode mode)
        {

            switch (mode)
            {
#pragma warning disable CA5394 // Do not use insecure randomness

                case BeeNodeSelectionMode.Random:
                    var healthyNodes = beeNodesStatus.Values.Where(status => status.IsAlive);
                    if (!healthyNodes.Any())
                        return null;

                    int takeIndex = rand.Next(0, healthyNodes.Count());
                    return healthyNodes.ElementAt(takeIndex);

#pragma warning restore CA5394 // Do not use insecure randomness

                case BeeNodeSelectionMode.RoundRobin:
                    BeeNodeStatus? selectedNode = null;

                    //take first node if last selected was null
                    if (lastSelectedNodeRoundRobin is null)
                        selectedNode = beeNodesStatus.Values.Where(status => status.IsAlive).FirstOrDefault();

                    //take next on list if already selected one previously
                    if (selectedNode is null)
                    {
                        var lastSelectedIndex = beeNodesStatus.Values
                            .Select((node, index) => new { index, node })
                            .Where(o => o.node == lastSelectedNodeRoundRobin)
                            .Select(o => o.index)
                            .FirstOrDefault();

                        selectedNode = beeNodesStatus.Values
                            .Skip(lastSelectedIndex + 1)
                            .Where(status => status.IsAlive)
                            .FirstOrDefault();
                    }

                    //or try from beginning
                    if (selectedNode is null)
                    {
                        selectedNode = beeNodesStatus.Values
                            .Where(status => status.IsAlive)
                            .FirstOrDefault();
                    }

                    //update last selected
                    lastSelectedNodeRoundRobin = selectedNode;

                    return selectedNode;

                default:
                    throw new InvalidOperationException();
            }
        }

        public void UpdateNodeInfo(BeeNode node)
        {
            // Try add. If already exists, original is kept.
            AddBeeNode(node);

            // Update info.
            beeNodesStatus[node.Id].UpdateInfo(node);
        }

        // Helpers.
        private async Task HeartbeatCallbackAsync()
        {
            foreach (var clientStatus in beeNodesStatus.Values)
            {
                try
                {
                    var result = await clientStatus.Client.DebugClient!.GetReadinessAsync();
                    clientStatus.IsAlive = result.Status == "ok";

                    //if alive and don't have an address, try to get it
                    if (clientStatus.IsAlive && clientStatus.EtherAddress is null)
                        backgroundJobClient.Enqueue<IRetrieveNodeAddressesTask>(task => task.RunAsync(clientStatus.Id));
                }
                catch (Exception e) when (
                    e is BeeNetDebugApiException ||
                    e is HttpRequestException ||
                    e is SocketException)
                {
                    clientStatus.IsAlive = false;
                }
            }
        }
    }
}

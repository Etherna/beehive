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
using Etherna.BeeNet;
using Etherna.BeeNet.Exceptions;
using Etherna.MongoDB.Driver;
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
    class BeeNodeClientsManager : IBeeNodeClientsManager, IDisposable
    {
        // Internal models.
        private class BeeNodeStatus
        {
            public BeeNodeStatus(string id, BeeNodeClient client)
            {
                Id = id;
                Client = client;
            }

            public string Id { get; }
            public BeeNodeClient Client { get; }
            public bool IsAlive { get; set; }
        }

        // Consts.
        private const int HeartbeatPeriod = 10000; //10s

        // Fields.
        private readonly IBeehiveDbContext beehiveDbContext;
        private Timer? heartbeatTimer;
        private BeeNodeStatus? lastNodeRoundRobinSelector;
        private readonly ConcurrentDictionary<string, BeeNodeStatus> nodeClientsStatus = new();
        private readonly Random rand = new();

        // Constructor and dispose.
        public BeeNodeClientsManager(
            IBeehiveDbContext beehiveDbContext)
        {
            this.beehiveDbContext = beehiveDbContext;
        }
        public void Dispose()
        {
            heartbeatTimer?.Dispose();
        }

        // Properties.
        public IEnumerable<BeeNodeClient> HealthyClients =>
            nodeClientsStatus.Values.Where(status => status.IsAlive)
                                    .Select(s => s.Client);

        // Methods.
        public async Task<BeeNodeClient> GetBeeNodeClientAsync(string nodeId)
        {
            if (nodeClientsStatus.ContainsKey(nodeId))
                return nodeClientsStatus[nodeId].Client;

            var beeNode = await beehiveDbContext.BeeNodes.FindOneAsync(nodeId);
            return AddNodeClient(beeNode);
        }

        public async Task LoadAllNodeClientsAsync()
        {
            var nodes = await beehiveDbContext.BeeNodes.QueryElementsAsync(
                elements => elements.ToListAsync());
            foreach (var node in nodes)
                AddNodeClient(node);
        }

        public bool RemoveBeeNodeClient(string nodeId) =>
            nodeClientsStatus.TryRemove(nodeId, out _);

        public void StartHealthHeartbeat() =>
            heartbeatTimer = new Timer(async _ => await HeartbeatCallbackAsync(), null, 0, HeartbeatPeriod);

        public void StopHealthHeartbeat() =>
            heartbeatTimer?.Change(Timeout.Infinite, 0);

        public BeeNodeClient? TrySelectHealthyNodeClientAsync(BeeNodeSelectionMode mode)
        {

            switch (mode)
            {
#pragma warning disable CA5394 // Do not use insecure randomness

                case BeeNodeSelectionMode.Random:
                    var healthyNodes = nodeClientsStatus.Values.Where(status => status.IsAlive);
                    if (!healthyNodes.Any())
                        return null;

                    int takeIndex = rand.Next(0, healthyNodes.Count());
                    return healthyNodes.ElementAt(takeIndex).Client;

#pragma warning restore CA5394 // Do not use insecure randomness

                case BeeNodeSelectionMode.RoundRobin:
                    BeeNodeStatus? selectedNode = null;

                    //take first node if last selected was null
                    if (lastNodeRoundRobinSelector is null)
                        selectedNode = nodeClientsStatus.Values.Where(status => status.IsAlive).FirstOrDefault();

                    //take next on list if already selected one previously
                    if (selectedNode is null)
                    {
                        var lastSelectedIndex = nodeClientsStatus.Values
                            .Select((node, index) => new { index, node })
                            .Where(o => o.node == lastNodeRoundRobinSelector)
                            .Select(o => o.index)
                            .FirstOrDefault();

                        selectedNode = nodeClientsStatus.Values
                            .Skip(lastSelectedIndex + 1)
                            .Where(status => status.IsAlive)
                            .FirstOrDefault();
                    }

                    //or try from beginning
                    if (selectedNode is null)
                    {
                        selectedNode = nodeClientsStatus.Values
                            .Where(status => status.IsAlive)
                            .FirstOrDefault();
                    }

                    lastNodeRoundRobinSelector = selectedNode;
                    return lastNodeRoundRobinSelector?.Client;

                default:
                    throw new InvalidOperationException();
            }
        }

        // Helpers.
        private BeeNodeClient AddNodeClient(BeeNode beeNode)
        {
            var client = new BeeNodeClient(beeNode.Url.AbsoluteUri, beeNode.GatewayPort, beeNode.DebugPort);
            nodeClientsStatus.TryAdd(beeNode.Id, new BeeNodeStatus(beeNode.Id, client));
            return client;
        }

        private async Task HeartbeatCallbackAsync()
        {
            foreach (var clientStatus in nodeClientsStatus.Values)
            {
                try
                {
                    var result = await clientStatus.Client.DebugClient!.GetReadinessAsync();
                    clientStatus.IsAlive = result.Status == "ok";
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

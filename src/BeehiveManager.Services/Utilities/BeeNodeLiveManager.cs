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
using Etherna.BeehiveManager.Services.Utilities.Models;
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
    /// Manage live instances of bee nodes
    /// </summary>
    class BeeNodeLiveManager : IBeeNodeLiveManager, IDisposable
    {
        // Consts.
        private const int HeartbeatPeriod = 10000; //10s

        // Fields.
        private readonly IBeehiveDbContext beehiveDbContext;
        private Timer? heartbeatTimer;
        private BeeNodeLiveInstance? lastSelectedNodeRoundRobin;
        private readonly ConcurrentDictionary<string, BeeNodeLiveInstance> beeNodeInstances = new(); //Id -> Live instance
        private readonly Random rand = new();

        // Constructor and dispose.
        public BeeNodeLiveManager(
            IBeehiveDbContext beehiveDbContext)
        {
            this.beehiveDbContext = beehiveDbContext;
        }
        public void Dispose()
        {
            heartbeatTimer?.Dispose();
        }

        // Properties.
        public IEnumerable<BeeNodeLiveInstance> AllNodes => beeNodeInstances.Values;
        public ChainState? ChainState { get; private set; }
        public IEnumerable<BeeNodeLiveInstance> HealthyNodes => AllNodes.Where(i => i.Status.IsAlive);

        // Methods.
        public async Task<BeeNodeLiveInstance> AddBeeNodeAsync(BeeNode beeNode)
        {
            // Add node.
            var liveInstance = new BeeNodeLiveInstance(beeNode, beehiveDbContext);
            var result = beeNodeInstances.TryAdd(beeNode.Id, liveInstance);

            // Refresh live status (if necessary).
            if (result)
                await liveInstance.TryRefreshStatusAsync();

            return beeNodeInstances[beeNode.Id];
        }

        public async Task<BeeNodeLiveInstance> GetBeeNodeLiveInstanceAsync(string nodeId)
        {
            if (beeNodeInstances.ContainsKey(nodeId))
                return beeNodeInstances[nodeId];

            var beeNode = await beehiveDbContext.BeeNodes.FindOneAsync(nodeId);
            return await AddBeeNodeAsync(beeNode);
        }

        public async Task LoadAllNodesAsync()
        {
            var nodes = await beehiveDbContext.BeeNodes.QueryElementsAsync(
                elements => elements.ToListAsync());
            foreach (var node in nodes)
                await AddBeeNodeAsync(node);
        }

        public bool RemoveBeeNode(string nodeId) =>
            beeNodeInstances.TryRemove(nodeId, out _);

        public void StartHealthHeartbeat() =>
            heartbeatTimer = new Timer(async _ => await HeartbeatCallbackAsync(), null, 0, HeartbeatPeriod);

        public void StopHealthHeartbeat() =>
            heartbeatTimer?.Change(Timeout.Infinite, 0);

        public BeeNodeLiveInstance? TrySelectHealthyNode(BeeNodeSelectionMode mode)
        {
            switch (mode)
            {
#pragma warning disable CA5394 // Do not use insecure randomness

                case BeeNodeSelectionMode.Random:
                    var healthyNodes = beeNodeInstances.Values.Where(instance => instance.Status.IsAlive);
                    if (!healthyNodes.Any())
                        return null;

                    int takeIndex = rand.Next(0, healthyNodes.Count());
                    return healthyNodes.ElementAt(takeIndex);

#pragma warning restore CA5394 // Do not use insecure randomness

                case BeeNodeSelectionMode.RoundRobin:
                    BeeNodeLiveInstance? selectedNode = null;

                    //take first node if last selected was null
                    if (lastSelectedNodeRoundRobin is null)
                        selectedNode = beeNodeInstances.Values.Where(instance => instance.Status.IsAlive).FirstOrDefault();

                    //take next on list if already selected one previously
                    if (selectedNode is null)
                    {
                        var lastSelectedIndex = beeNodeInstances.Values
                            .Select((node, index) => new { index, node })
                            .Where(o => o.node == lastSelectedNodeRoundRobin)
                            .Select(o => o.index)
                            .FirstOrDefault();

                        selectedNode = beeNodeInstances.Values
                            .Skip(lastSelectedIndex + 1)
                            .Where(instance => instance.Status.IsAlive)
                            .FirstOrDefault();
                    }

                    //or try from beginning
                    if (selectedNode is null)
                    {
                        selectedNode = beeNodeInstances.Values
                            .Where(instance => instance.Status.IsAlive)
                            .FirstOrDefault();
                    }

                    //update last selected
                    lastSelectedNodeRoundRobin = selectedNode;

                    return selectedNode;

                default:
                    throw new InvalidOperationException();
            }
        }

        // Helpers.
        private async Task HeartbeatCallbackAsync()
        {
            var tasks = new List<Task>();

            //update nodes
            foreach (var instance in beeNodeInstances.Values)
                tasks.Add(instance.TryRefreshStatusAsync());
            await Task.WhenAll(tasks);

            //update chain state
            var node = TrySelectHealthyNode(BeeNodeSelectionMode.RoundRobin);
            if (node is not null)
            {
                try
                {
                    ChainState = new ChainState(node.Id, await node.Client.DebugClient!.GetChainStateAsync());
                }
                catch (Exception e) when (
                    e is BeeNetDebugApiException ||
                    e is HttpRequestException ||
                    e is SocketException)
                { }
            }
        }
    }
}

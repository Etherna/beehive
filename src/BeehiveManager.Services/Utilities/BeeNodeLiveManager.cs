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
using Etherna.BeehiveManager.Services.Extensions;
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
        private readonly IBeehiveDbContext dbContext;
        private Timer? heartbeatTimer;
        private readonly Dictionary<string, BeeNodeLiveInstance?> lastSelectedNodesRoundRobin = new(); //selectionContext -> lastSelectedNodeRoundRobin
        private readonly ConcurrentDictionary<string, BeeNodeLiveInstance> beeNodeInstances = new(); //Id -> Live instance

        // Constructor and dispose.
        public BeeNodeLiveManager(
            IBeehiveDbContext dbContext)
        {
            this.dbContext = dbContext;
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
            var liveInstance = new BeeNodeLiveInstance(beeNode);
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

            var beeNode = await dbContext.BeeNodes.FindOneAsync(nodeId);
            return await AddBeeNodeAsync(beeNode);
        }

        public BeeNodeLiveInstance GetBeeNodeLiveInstanceByOwnedPostageBatch(string batchId) =>
            AllNodes.First(n => n.Status.PostageBatchesId?.Contains(batchId) ?? false);

        public IEnumerable<BeeNodeLiveInstance> GetBeeNodeLiveInstancesByPinnedContent(string hash, bool requireAliveNodes) =>
            AllNodes.Where(n => (n.Status.PinnedHashes?.Contains(hash) ?? false) &&
                                (!requireAliveNodes || n.Status.IsAlive));

        public async Task LoadAllNodesAsync()
        {
            var nodes = await dbContext.BeeNodes.QueryElementsAsync(
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

        public async Task<BeeNodeLiveInstance?> TrySelectHealthyNodeAsync(
            BeeNodeSelectionMode mode,
            string? selectionContext = null,
            Func<BeeNodeLiveInstance, Task<bool>>? isValidPredicate = null)
        {
            isValidPredicate ??= _ => Task.FromResult(true);
            selectionContext ??= "";

            switch (mode)
            {

                case BeeNodeSelectionMode.Random:
                    var availableNodes = beeNodeInstances.Values.Where(instance => instance.Status.IsAlive).ToList();

                    while (availableNodes.Any())
                    {
#pragma warning disable CA5394 // Do not use insecure randomness
                        int takeIndex = Random.Shared.Next(0, availableNodes.Count);
#pragma warning restore CA5394 // Do not use insecure randomness
                        var node = availableNodes[takeIndex];

                        //validate node
                        if (!await isValidPredicate(node))
                        {
                            availableNodes.RemoveAt(takeIndex); //lazy check for efficiency
                            continue;
                        }

                        return node;
                    }
                    return null;

                case BeeNodeSelectionMode.RoundRobin:
                    BeeNodeLiveInstance? selectedNode = null;

                    if (!lastSelectedNodesRoundRobin.ContainsKey(selectionContext)) //take first node if never selected once in this context
                    {
                        selectedNode = await beeNodeInstances.Values
                            .Where(async instance => instance.Status.IsAlive && await isValidPredicate(instance))
                            .FirstOrDefaultAsync();
                    }
                    else //take next on list if already selected one previously
                    {
                        var lastSelectedNodeWithIndexList = beeNodeInstances.Values
                            .Select((node, index) => new { index, node })
                            .Where(g => g.node == lastSelectedNodesRoundRobin[selectionContext]);

                        if (lastSelectedNodeWithIndexList.Any()) //if prev node still exists
                        {
                            selectedNode = await beeNodeInstances.Values
                                .Skip(lastSelectedNodeWithIndexList.First().index + 1)
                                .Where(async instance => instance.Status.IsAlive && await isValidPredicate(instance))
                                .FirstOrDefaultAsync();
                        }

                        //or try from beginning
                        selectedNode ??= await beeNodeInstances.Values
                            .Where(async instance => instance.Status.IsAlive && await isValidPredicate(instance))
                            .FirstOrDefaultAsync();
                    }

                    //update last selected, if not null
                    if (selectedNode is not null)
                        lastSelectedNodesRoundRobin[selectionContext] = selectedNode;

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
            var node = await TrySelectHealthyNodeAsync(BeeNodeSelectionMode.RoundRobin, "chainState");
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

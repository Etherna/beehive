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

using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.BeeNet.Exceptions;
using Etherna.BeeNet.Models;
using Etherna.MongoDB.Driver.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ChainState = Etherna.Beehive.Services.Utilities.Models.ChainState;

namespace Etherna.Beehive.Services.Utilities
{
    /// <summary>
    /// Manage live instances of bee nodes
    /// </summary>
    internal sealed class BeeNodeLiveManager(IBeehiveDbContext dbContext)
        : IBeeNodeLiveManager, IDisposable
    {
        // Consts.
        private const int HeartbeatPeriod = 10000; //10s

        // Fields.
        private Timer? heartbeatTimer;
        private readonly Dictionary<string, BeeNodeLiveInstance?> lastSelectedNodesRoundRobin = new(); //selectionContext -> lastSelectedNodeRoundRobin
        private readonly ConcurrentDictionary<string, BeeNodeLiveInstance> beeNodeInstances = new(); //Id -> Live instance

        // Dispose.
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
            if (beeNodeInstances.TryGetValue(nodeId, out var instance))
                return instance;

            var beeNode = await dbContext.BeeNodes.FindOneAsync(nodeId);
            return await AddBeeNodeAsync(beeNode);
        }

        public BeeNodeLiveInstance GetBeeNodeLiveInstanceByOwnedPostageBatch(PostageBatchId batchId) =>
            AllNodes.First(n => n.Status.PostageBatchesId.Contains(batchId));

        public IEnumerable<BeeNodeLiveInstance> GetBeeNodeLiveInstancesByPinnedContent(string hash, bool requireAliveNodes) =>
            AllNodes.Where(n => n.Status.PinnedHashes.Contains(hash) &&
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

                    while (availableNodes.Count > 0)
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

                    if (!lastSelectedNodesRoundRobin.TryGetValue(selectionContext, out BeeNodeLiveInstance? lastNode)) //take first node if never selected once in this context
                    {
                        selectedNode = await beeNodeInstances.Values
                            .Where(async instance => instance.Status.IsAlive && await isValidPredicate(instance))
                            .FirstOrDefaultAsync();
                    }
                    else //take next on list if already selected one previously
                    {
                        var lastSelectedNodeWithIndexList = beeNodeInstances.Values
                            .Select((node, index) => new { index, node })
                            .Where(g => g.node == lastNode)
                            .ToList();

                        if (lastSelectedNodeWithIndexList.Count > 0) //if prev node still exists
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
                    ChainState = new ChainState(node.Id, await node.Client.GetChainStateAsync());
                }
                catch (Exception e) when (
                    e is BeeNetApiException ||
                    e is HttpRequestException ||
                    e is SocketException)
                { }
            }
        }
    }
}

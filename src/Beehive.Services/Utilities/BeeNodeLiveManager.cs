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
using Etherna.Beehive.Services.Options;
using Etherna.Beehive.Services.Utilities.DevNode;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver.Linq;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChainState = Etherna.Beehive.Services.Utilities.Models.ChainState;

namespace Etherna.Beehive.Services.Utilities
{
    /// <summary>
    /// Manage live instances of bee nodes
    /// </summary>
    /// <remarks>
    /// A singleton: the db context is scoped, so every db access opens a scope of its own
    /// instead of capturing a context for the process lifetime.
    /// The heartbeat round is the only flow calling the nodes for their status: a node is registered without being
    /// called, and the round asks every node in parallel, each call within the status call timeout.
    /// </remarks>
    internal sealed class BeeNodeLiveManager(
        IOptions<DevNodeOptions> devNodeOptions,
        ILogger<BeeNodeLiveManager> logger,
        IServiceScopeFactory serviceScopeFactory)
        : IBeeNodeLiveManager, IDisposable
    {
        // Consts.
        private readonly TimeSpan HeartbeatPeriod = TimeSpan.FromSeconds(10);

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
        public async Task<BeeNodeLiveInstance> GetBeeNodeLiveInstanceAsync(string nodeId)
        {
            if (beeNodeInstances.TryGetValue(nodeId, out var instance))
                return instance;

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IBeehiveDbContext>();

            var beeNode = await dbContext.BeeNodes.FindOneAsync(nodeId);
            return TryAddBeeNode(beeNode);
        }

        public async Task<BeeNodeLiveInstance> SelectHealthyNodeAsync(
            BeeNodeSelectionMode mode = BeeNodeSelectionMode.RoundRobin,
            string? selectionContext = null,
            Func<BeeNodeLiveInstance, Task<bool>>? isValidPredicate = null)
        {
            var node = await TrySelectHealthyNodeAsync(mode, selectionContext, isValidPredicate);
            return node ?? throw new InvalidOperationException();
        }

        public BeeNodeLiveInstance SelectNearestHealthyNode(SwarmHash hash)
        {
            // Select the closest healthy node.
            var healthyNodes = HealthyNodes.Where(n => n.Status.Addresses != null).ToArray();
            if (healthyNodes.Length == 0)
                throw new InvalidOperationException("No healthy nodes found.");
            
            var closest = healthyNodes[0];
            for (var i = 1; i < healthyNodes.Length; i++)
            {
                if (SwarmHash.CompareDistances(
                        closest.Status.Addresses!.Overlay.ToReadOnlyMemory().Span,
                        healthyNodes[i].Status.Addresses!.Overlay.ToReadOnlyMemory().Span,
                        hash.ToReadOnlyMemory().Span) > 0)
                    closest = healthyNodes[i];
            }
            
            return closest;
        }

        public void StartHealthHeartbeat() =>
            heartbeatTimer = new Timer(async _ =>
                await HeartbeatCallbackAsync(), null, 0, (int)HeartbeatPeriod.TotalMilliseconds);

        public void StopHealthHeartbeat() =>
            heartbeatTimer?.Change(Timeout.Infinite, 0);
        
        public BeeNodeLiveInstance TryAddBeeNode(BeeNode beeNode)
        {
            // The node is registered without being called: its status comes from the heartbeat, asked for a round now.
            if (TryAddBeeNodeInstance(beeNode, out var liveInstance))
                RequestHeartbeat();
            return liveInstance;
        }

        public bool TryRemoveBeeNode(string nodeId)
        {
            var removed = TryRemoveBeeNodeInstance(nodeId);
            if (removed)
                RequestHeartbeat(); //restores the dev node when no more nodes are registered
            return removed;
        }

        public async Task<BeeNodeLiveInstance?> TrySelectHealthyNodeAsync(
            BeeNodeSelectionMode mode = BeeNodeSelectionMode.RoundRobin,
            string? selectionContext = null,
            Func<BeeNodeLiveInstance, Task<bool>>? isValidPredicate = null)
        {
            isValidPredicate ??= _ => Task.FromResult(true);
            selectionContext ??= "";

            switch (mode)
            {

                case BeeNodeSelectionMode.Random:
                    var availableNodes = AllNodes.Where(instance => instance.Status.IsAlive).ToList();

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
                        selectedNode = await AllNodes
                            .Where(async instance => instance.Status.IsAlive && await isValidPredicate(instance))
                            .FirstOrDefaultAsync();
                    }
                    else //take next on list if already selected one previously
                    {
                        var lastSelectedNodeWithIndexList = AllNodes
                            .Select((node, index) => new { index, node })
                            .Where(g => g.node == lastNode)
                            .ToList();

                        if (lastSelectedNodeWithIndexList.Count > 0) //if prev node still exists
                        {
                            selectedNode = await AllNodes
                                .Skip(lastSelectedNodeWithIndexList.First().index + 1)
                                .Where(async instance => instance.Status.IsAlive && await isValidPredicate(instance))
                                .FirstOrDefaultAsync();
                        }

                        //or try from beginning
                        selectedNode ??= await AllNodes
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
            // Sync the registered nodes with the db.
            List<BeeNode> dbNodes;
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IBeehiveDbContext>();

                dbNodes = await dbContext.BeeNodes.QueryElementsAsync(nodes => nodes.ToListAsync());
            }
            catch (UnauthorizedAccessException e) //can fail during dbcontext migration
            {
                logger.BeeNodesHeartbeatSkipped(e);
                return;
            }

            //add new nodes from db
            foreach (var dbNode in dbNodes)
                TryAddBeeNodeInstance(dbNode, out _);
            //remove missing nodes from db (dev node is not persisted, skip it)
            foreach (var instance in AllNodes.Where(n =>
                         n.Id != DevNodeClient.NodeId &&
                         !dbNodes.Select(dbN => dbN.Id).Contains(n.Id)).ToArray())
                TryRemoveBeeNodeInstance(instance.Id);
            //fall back to the dev node when no node is registered
            TryAddDevNodeInstance();

            // Refresh nodes status, in parallel: a node not answering holds its own refresh only, within the timeout.
            await Task.WhenAll(beeNodeInstances.Values.Select(instance => instance.TryRefreshStatusAsync()));

            // Update chain state.
            var node = await TrySelectHealthyNodeAsync(BeeNodeSelectionMode.RoundRobin, "chainState");
            if (node is not null)
            {
                try
                {
                    ChainState = new ChainState(node.Id, await node.GetChainStateAsync());
                }
                catch (Exception e) when (e.IsBeeNodeCallFailure())
                {
                    logger.BeeNodeCallFailed(node.Id, "getChainState", e.GetBeeNodeFailureReason(), e);
                }
            }
        }

        /// <summary>
        /// Run a heartbeat round now, instead of waiting the next tick: nothing before the heartbeat is started
        /// </summary>
        private void RequestHeartbeat() =>
            heartbeatTimer?.Change(TimeSpan.Zero, HeartbeatPeriod);

        private bool TryAddBeeNodeInstance(BeeNode beeNode, out BeeNodeLiveInstance liveInstance)
        {
            if (beeNodeInstances.TryGetValue(beeNode.Id, out var existingInstance))
            {
                liveInstance = existingInstance;
                return false;
            }

            //remove the dev node when a real node is registered
            if (beeNodeInstances.TryRemove(DevNodeClient.NodeId, out _))
                logger.DevNodeDisabled();

            var newInstance = new BeeNodeLiveInstance(beeNode, logger);
            if (beeNodeInstances.TryAdd(beeNode.Id, newInstance))
            {
                liveInstance = newInstance;
                return true;
            }
            liveInstance = beeNodeInstances[beeNode.Id]; //registered meanwhile
            return false;
        }

        private void TryAddDevNodeInstance()
        {
            //add dev node only if other real nodes don't exist
            if (!devNodeOptions.Value.Enabled || !beeNodeInstances.IsEmpty)
                return;

            if (beeNodeInstances.TryAdd(DevNodeClient.NodeId, new BeeNodeLiveInstance(new DevNodeClient(), logger)))
                logger.DevNodeEnabled();
        }

        private bool TryRemoveBeeNodeInstance(string nodeId)
        {
            var removed = beeNodeInstances.TryRemove(nodeId, out _);
            if (removed)
                logger.BeeNodeLiveInstanceRemoved(nodeId);
            return removed;
        }
    }
}

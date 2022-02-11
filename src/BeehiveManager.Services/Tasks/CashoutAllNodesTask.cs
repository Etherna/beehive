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
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Exceptions;
using Etherna.MongoDB.Driver;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class CashoutAllNodesTask : ICashoutAllNodesTask
    {
        // Consts.
        public const string TaskId = "cashoutAllNodesTask";
        public const long MinAmount = 100_000_000_000_000; //10^14, 0.01 BZZ

        // Fields.
        private readonly IBeeNodesStatusManager beeNodesStatusManager;
        private readonly IBeehiveDbContext context;

        // Constructors.
        public CashoutAllNodesTask(
            IBeeNodesStatusManager beeNodesStatusManager,
            IBeehiveDbContext context)
        {
            this.beeNodesStatusManager = beeNodesStatusManager;
            this.context = context;
        }

        // Methods.
        public async Task RunAsync()
        {
            // List all nodes.
            await context.BeeNodes.AccessToCollectionAsync(collection => collection
                .Find(FilterDefinition<BeeNode>.Empty, new FindOptions { NoCursorTimeout = true })
                .ForEachAsync(async node =>
                {
                    // Get info.
                    var nodeStatus = await beeNodesStatusManager.GetBeeNodeStatusAsync(node.Id);
                    var nodeClient = nodeStatus.Client;
                    if (nodeClient.DebugClient is null) //skip if doesn't have a debug api config
                        return;

                    var totalCashedout = 0L;
                    var txs = new List<string>();
                    try
                    {
                        // Enumerate peers.
                        var cheques = await nodeClient.DebugClient.GetAllChequeBookChequesAsync();
                        foreach (var peer in cheques.Select(c => c.Peer))
                        {
                            var uncashedAmount = 0L;

                            try
                            {
                                var cashoutResponse = await nodeClient.DebugClient.GetChequeBookCashoutForPeerAsync(peer);
                                uncashedAmount = cashoutResponse.UncashedAmount;
                            }
                            catch (BeeNetDebugApiException) { }

                            // Cashout.
                            if (uncashedAmount >= MinAmount)
                            {
                                try
                                {
                                    var txHash = await nodeClient.DebugClient.CashoutChequeForPeerAsync(peer);
                                    totalCashedout += uncashedAmount;
                                    txs.Add(txHash);
                                }
                                catch (BeeNetDebugApiException) { }
                            }
                        }
                    }
                    catch (BeeNetDebugApiException) { return; } //issues contacting the node instance api
                    catch (HttpRequestException) { return; }

                    // Add log.
                    if (totalCashedout > 0)
                    {
                        var log = new CashoutNodeLog(node, txs, totalCashedout);
                        await context.NodeLogs.CreateAsync(log);
                    }
                }));
        }
    }
}

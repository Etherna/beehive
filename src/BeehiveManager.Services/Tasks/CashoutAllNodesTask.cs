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
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Exceptions;
using Etherna.MongoDB.Driver;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class CashoutAllNodesTask : ICashoutAllNodesTask
    {
        // Consts.
        public const string TaskId = "cashoutAllNodesTask";
        public const long MinAmount = 1_000_000_000_000; //10^12 = 0.0001 BZZ

        // Fields.
        private readonly IBeeNodeLiveManager beeNodeLiveManager;
        private readonly IBeehiveDbContext context;
        private readonly ILogger<CashoutAllNodesTask> logger;

        // Constructors.
        public CashoutAllNodesTask(
            IBeeNodeLiveManager beeNodeLiveManager,
            IBeehiveDbContext context,
            ILogger<CashoutAllNodesTask> logger)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
            this.context = context;
            this.logger = logger;
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
                    var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(node.Id);
                    var nodeClient = beeNodeInstance.Client;
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
                        logger.NodeCashedOut(node.Id, totalCashedout, txs);
                }));
        }
    }
}

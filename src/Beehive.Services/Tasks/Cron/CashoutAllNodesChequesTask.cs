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

using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Options;
using Etherna.Beehive.Services.Utilities;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Cron
{
    public class CashoutAllNodesChequesTask(
        IBeeNodeLiveManager liveManager,
        ILogger<CashoutAllNodesChequesTask> logger,
        IOptions<CashoutAllNodesChequesOptions> options)
        : ICashoutAllNodesChequesTask
    {
        // Consts.
        public const string TaskId = "cashoutAllNodesTask";

        // Fields.
        private readonly CashoutAllNodesChequesOptions options = options.Value;

        // Methods.
        public async Task RunAsync()
        {
            var nodesCount = 0;
            BzzValue allNodesBzzCashedOut = 0;
            foreach (var node in liveManager.AllNodes)
            {
                BzzValue totalBzzCashedOut = 0;
                var txs = new List<EthTxHash>();
                try
                {
                    // Enumerate peers.
                    var cheques = await node.Client.GetAllChequebookChequesAsync();
                    foreach (var peer in cheques.Select(c => c.Peer))
                    {
                        BzzValue? uncashedBzzAmount = null;
                        try
                        {
                            var cashoutResponse = await node.Client.GetChequebookCashoutForPeerAsync(peer);
                            uncashedBzzAmount = cashoutResponse.UncashedAmount;
                        }
                        catch (SwarmSdkApiException e) when (e.StatusCode == 404)
                        { }

                        // Cashout.
                        if (uncashedBzzAmount >= options.BzzMaxTrigger)
                        {
                            try
                            {
                                var txHash = await node.Client.CashoutChequeForPeerAsync(peer);
                                totalBzzCashedOut += uncashedBzzAmount.Value;
                                txs.Add(txHash);
                            }
                            catch (SwarmSdkApiException e)
                            {
                                logger.ChequeCashoutFailed(node.Id, peer, e);
                            }
                        }
                    }
                }
                catch (Exception e) when (e is SwarmSdkApiException or HttpRequestException) //issues contacting the node instance api
                {
                    logger.BeeNodeCallFailed(node.Id, "cashoutCheques", e.GetBeeNodeFailureReason(), e);
                    return;
                }

                // Add log.
                if (totalBzzCashedOut > 0)
                    logger.NodeCashedOut(node.Id, totalBzzCashedOut, txs);

                nodesCount++;
                allNodesBzzCashedOut += totalBzzCashedOut;
            }

            logger.CashoutAllNodesChequesTaskCompleted(nodesCount, allNodesBzzCashedOut);
        }
    }
}

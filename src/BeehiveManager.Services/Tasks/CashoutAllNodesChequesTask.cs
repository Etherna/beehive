﻿// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeehiveManager.Services.Extensions;
using Etherna.BeehiveManager.Services.Settings;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Exceptions;
using Etherna.BeeNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Web3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class CashoutAllNodesChequesTask : ICashoutAllNodesChequesTask
    {
        // Consts.
        public const string TaskId = "cashoutAllNodesTask";

        // Fields.
        private readonly IBeeNodeLiveManager liveManager;
        private readonly ILogger<CashoutAllNodesChequesTask> logger;
        private readonly CashoutAllNodesChequesSettings options;

        // Constructors.
        public CashoutAllNodesChequesTask(
            IBeeNodeLiveManager liveManager,
            ILogger<CashoutAllNodesChequesTask> logger,
            IOptions<CashoutAllNodesChequesSettings> options)
        {
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            this.liveManager = liveManager;
            this.logger = logger;
            this.options = options.Value;
        }

        // Methods.
        public async Task RunAsync()
        {
            foreach (var node in liveManager.AllNodes)
            {
                BzzBalance totalBzzCashedOut = 0;
                var txs = new List<string>();
                try
                {
                    // Enumerate peers.
                    var cheques = await node.Client.GetAllChequebookChequesAsync();
                    foreach (var peer in cheques.Select(c => c.Peer))
                    {
                        BzzBalance? uncashedBzzAmount = null;
                        try
                        {
                            var cashoutResponse = await node.Client.GetChequebookCashoutForPeerAsync(peer);
                            uncashedBzzAmount = cashoutResponse.UncashedAmount;
                        }
                        catch (BeeNetApiException) { }

                        // Cashout.
                        if (uncashedBzzAmount >= options.BzzMaxTrigger)
                        {
                            try
                            {
                                var txHash = await node.Client.CashoutChequeForPeerAsync(peer);
                                totalBzzCashedOut += uncashedBzzAmount.Value;
                                txs.Add(txHash);
                            }
                            catch (BeeNetApiException) { }
                        }
                    }
                }
                catch (BeeNetApiException) { return; } //issues contacting the node instance api
                catch (HttpRequestException) { return; }

                // Add log.
                if (totalBzzCashedOut > 0)
                    logger.NodeCashedOut(node.Id, totalBzzCashedOut, txs);
            }
        }
    }
}

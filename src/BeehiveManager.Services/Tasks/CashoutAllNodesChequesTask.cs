//   Copyright 2021-present Etherna SA
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

using Etherna.BeehiveManager.Services.Extensions;
using Etherna.BeehiveManager.Services.Settings;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Exceptions;
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

        private const int BzzDecimalPlaces = 16;

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
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            this.liveManager = liveManager;
            this.logger = logger;
            this.options = options.Value;
        }

        // Methods.
        public async Task RunAsync()
        {
            foreach (var node in liveManager.AllNodes)
            {
                if (node.Client.DebugClient is null)
                    continue;

                decimal totalBzzCashedOut = 0;
                var txs = new List<string>();
                try
                {
                    // Enumerate peers.
                    var cheques = await node.Client.DebugClient.GetAllChequeBookChequesAsync();
                    foreach (var peer in cheques.Select(c => c.Peer))
                    {
                        decimal? uncashedBzzAmount = null;
                        try
                        {
                            var cashoutResponse = await node.Client.DebugClient.GetChequeBookCashoutForPeerAsync(peer);
                            uncashedBzzAmount = Web3.Convert.FromWei(cashoutResponse.UncashedAmount, BzzDecimalPlaces);
                        }
                        catch (BeeNetDebugApiException) { }

                        // Cashout.
                        if (uncashedBzzAmount >= options.BzzMaxTrigger)
                        {
                            try
                            {
                                var txHash = await node.Client.DebugClient.CashoutChequeForPeerAsync(peer);
                                totalBzzCashedOut += uncashedBzzAmount.Value;
                                txs.Add(txHash);
                            }
                            catch (BeeNetDebugApiException) { }
                        }
                    }
                }
                catch (BeeNetDebugApiException) { return; } //issues contacting the node instance api
                catch (HttpRequestException) { return; }

                // Add log.
                if (totalBzzCashedOut > 0)
                    logger.NodeCashedOut(node.Id, totalBzzCashedOut, txs);
            }
        }
    }
}

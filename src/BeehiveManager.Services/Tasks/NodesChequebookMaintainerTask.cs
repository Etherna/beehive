// Copyright 2021-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Etherna.BeehiveManager.Services.Extensions;
using Etherna.BeehiveManager.Services.Settings;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Web3;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class NodesChequebookMaintainerTask : INodesChequebookMaintainerTask
    {
        // Consts.
        public const string TaskId = "nodesChequebookMaintainerTask";

        // Fields.
        private readonly bool isEnabled;
        private readonly IBeeNodeLiveManager liveManager;
        private readonly ILogger<NodesChequebookMaintainerTask> logger;
        private readonly NodesChequebookMaintainerSettings options;

        // Constructor.
        public NodesChequebookMaintainerTask(
            IBeeNodeLiveManager liveManager,
            ILogger<NodesChequebookMaintainerTask> logger,
            IOptions<NodesChequebookMaintainerSettings> options)
        {
            ArgumentNullException.ThrowIfNull(options, nameof(options));

            this.liveManager = liveManager;
            this.logger = logger;
            this.options = options.Value;

            isEnabled = this.options.RunDeposits || this.options.RunWithdraws;
        }

        // Methods.
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Each exception needs to be catched and logged without stop task")]
        public async Task RunAsync()
        {
            if (!isEnabled)
                return;

            foreach (var node in liveManager.AllNodes)
            {
                BzzBalance? availableBzzBalance = null;
                try
                {
                    var chequebookBalanceDto = await node.Client.GetChequebookBalanceAsync();
                    availableBzzBalance = chequebookBalanceDto.AvailableBalance;
                }
                catch { }

                // Deposit.
                if (options.RunDeposits &&
                    availableBzzBalance < options.BzzMinTrigger)
                {
                    var bzzDepositAmount = options.BzzTargetAmount!.Value - availableBzzBalance.Value;

                    try
                    {
                        var tx = await node.Client.DepositIntoChequebookAsync(bzzDepositAmount);
                        logger.SuccededToDepositBzzOnNodeChequeBook(node.Id, bzzDepositAmount, tx);
                    }
                    catch (Exception ex)
                    {
                        logger.FailedToDepositBzzOnNodeChequeBook(node.Id, bzzDepositAmount, ex);
                    }
                }

                // Withdraw.
                else if (options.RunWithdraws &&
                    availableBzzBalance > options.BzzMaxTrigger)
                {
                    var bzzWithdrawAmount = availableBzzBalance.Value - options.BzzTargetAmount!.Value;

                    try
                    {
                        var tx = await node.Client.WithdrawFromChequebookAsync(bzzWithdrawAmount);
                        logger.SuccededToWithdrawBzzOnNodeChequeBook(node.Id, bzzWithdrawAmount, tx);
                    }
                    catch (Exception ex)
                    {
                        logger.FailedToWithdrawBzzOnNodeChequeBook(node.Id, bzzWithdrawAmount, ex);
                    }
                }
            }
        }
    }
}

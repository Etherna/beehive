// Copyright 2021-present Etherna SA
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

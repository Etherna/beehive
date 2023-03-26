using Etherna.BeehiveManager.Services.Extensions;
using Etherna.BeehiveManager.Services.Settings;
using Etherna.BeehiveManager.Services.Utilities;
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

        private const int BzzDecimalPlaces = 16;

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
            if (options is null)
                throw new ArgumentNullException(nameof(options));

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
                if (node.Client.DebugClient is null)
                    continue;

                decimal? availableBzzBalance = null;
                try
                {
                    var chequebookBalanceDto = await node.Client.DebugClient.GetChequeBookBalanceAsync();
                    availableBzzBalance = Web3.Convert.FromWei(chequebookBalanceDto.AvailableBalance, BzzDecimalPlaces);
                }
                catch { }

                // Deposit.
                if (options.RunDeposits &&
                    availableBzzBalance < options.BzzMinTrigger)
                {
                    var bzzDepositAmount = options.BzzTargetAmount!.Value - availableBzzBalance.Value;

                    try
                    {
                        var tx = await node.Client.DebugClient.DepositIntoChequeBookAsync((long)Web3.Convert.ToWei(bzzDepositAmount, BzzDecimalPlaces));
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
                        var tx = await node.Client.DebugClient.WithdrawFromChequeBookAsync((long)Web3.Convert.ToWei(bzzWithdrawAmount, BzzDecimalPlaces));
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

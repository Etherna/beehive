using Etherna.BeehiveManager.Services.Extensions;
using Etherna.BeehiveManager.Services.Settings;
using Etherna.BeehiveManager.Services.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Contracts.Standards.ERC20.ContractDefinition;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class FundNodesTask : IFundNodesTask, IDisposable
    {
        // Consts.
        public const string TaskId = "fundNodesTask";

        private const int BzzDecimalPlaces = 16;

        // Fields.
        private bool disposed;
        private readonly bool isEnabled;
        private readonly IBeeNodeLiveManager liveManager;
        private readonly ILogger<FundNodesTask> logger;
        private readonly FundNodesSettings options;
        private readonly Web3? tresureChestWeb3;
        private readonly WebSocketClient? websocketClient;

        // Constructor.
        public FundNodesTask(
            IBeeNodeLiveManager liveManager,
            ILogger<FundNodesTask> logger,
            IOptions<FundNodesSettings> options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            this.liveManager = liveManager;
            this.logger = logger;
            this.options = options.Value;

            if (this.options.RunBzzFunding || this.options.RunXDaiFunding)
            {
                isEnabled = true;
                if (this.options.WebsocketEndpoint is not null)
                {
                    websocketClient = new WebSocketClient(this.options.WebsocketEndpoint);
                    tresureChestWeb3 = new Web3(new Account(this.options.ChestPrivateKey, this.options.ChainId), websocketClient);
                }
                else if (this.options.RPCEndpoint is not null)
                {
                    var rpcClient = new RpcClient(new Uri(this.options.RPCEndpoint));
                    tresureChestWeb3 = new Web3(new Account(this.options.ChestPrivateKey, this.options.ChainId), rpcClient);
                }
                else throw new InvalidOperationException();
            }
            else isEnabled = false;
        }

        // Dispose.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;

            // Dispose managed resources.
            if (disposing)
                websocketClient?.Dispose();

            disposed = true;
        }

        // Methods.
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Each exception needs to be catched and logged without stop task")]
        public async Task RunAsync()
        {
            if (!isEnabled)
                return;

            // For each node, even if actually offline.
            foreach (var node in liveManager.AllNodes)
            {
                if (node.Status.Addresses is null)
                    continue;

                // Bzz funding.
                if (options.RunBzzFunding)
                {
                    // Get node amount.
                    decimal? bzzNodeAmount = null;
                    try
                    {
                        var balanceOfFunctionMessage = new BalanceOfFunction()
                        {
                            Owner = node.Status.Addresses.Ethereum
                        };
                        var balanceHandler = tresureChestWeb3!.Eth.GetContractQueryHandler<BalanceOfFunction>();
                        var plurBalance = await balanceHandler.QueryAsync<BigInteger>(options.BzzContractAddress, balanceOfFunctionMessage);
                        bzzNodeAmount = Web3.Convert.FromWei(plurBalance, 16);
                    }
                    catch { }

                    // Fund node.
                    if (bzzNodeAmount < options.BzzMinTrigger)
                    {
                        var bzzFundAmount = options.BzzTargetAmount!.Value - bzzNodeAmount.Value;
                        try
                        {
                            var transferHandler = tresureChestWeb3!.Eth.GetContractTransactionHandler<TransferFunction>();
                            var transferFunctionMessage = new TransferFunction()
                            {
                                To = node.Status.Addresses.Ethereum,
                                Value = Web3.Convert.ToWei(bzzFundAmount, BzzDecimalPlaces)
                            };
                            var tx = await transferHandler.SendRequestAndWaitForReceiptAsync(options.BzzContractAddress, transferFunctionMessage);

                            if (tx.Succeeded())
                                logger.SuccededToFundBzzOnNode(node.Id, bzzFundAmount, bzzNodeAmount.Value + bzzFundAmount, tx.TransactionHash);
                            else
                                logger.FailedToFundBzzOnNode(node.Id, bzzFundAmount, tx.TransactionHash, null);
                        }
                        catch (Exception ex)
                        {
                            logger.FailedToFundBzzOnNode(node.Id, bzzFundAmount, null, ex);
                        }
                    }
                }

                // xDai funding.
                if (options.RunXDaiFunding)
                {
                    // Get node amount.
                    decimal? xDaiNodeAmount = null;
                    try
                    {
                        var weiBalance = await tresureChestWeb3!.Eth.GetBalance.SendRequestAsync(node.Status.Addresses.Ethereum);
                        xDaiNodeAmount = Web3.Convert.FromWei(weiBalance);
                    }
                    catch { }

                    // Fund node.
                    if (xDaiNodeAmount < options.XDaiMinTrigger)
                    {
                        var xDaiFundAmount = options.XDaiTargetAmount!.Value - xDaiNodeAmount.Value;
                        try
                        {
                            var tx = await tresureChestWeb3!.Eth.GetEtherTransferService()
                                .TransferEtherAndWaitForReceiptAsync(node.Status.Addresses.Ethereum, xDaiFundAmount);

                            if (tx.Succeeded())
                                logger.SuccededToFundXDaiOnNode(node.Id, xDaiFundAmount, xDaiNodeAmount.Value + xDaiFundAmount, tx.TransactionHash);
                            else
                                logger.FailedToFundXDaiOnNode(node.Id, xDaiFundAmount, tx.TransactionHash, null);
                        }
                        catch (Exception ex)
                        {
                            logger.FailedToFundXDaiOnNode(node.Id, xDaiFundAmount, null, ex);
                        }
                    }
                }
            }
        }
    }
}

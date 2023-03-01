using Etherna.BeehiveManager.Services.Extensions;
using Etherna.BeehiveManager.Services.Settings;
using Etherna.BeehiveManager.Services.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.Web3.Accounts;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class FundNodesTask : IFundNodesTask
    {
        // Consts.
        public const string TaskId = "fundNodesTask";

        // Fields.
        private readonly IBeeNodeLiveManager liveManager;
        private readonly ILogger<FundNodesTask> logger;
        private readonly FundNodesSettings options;

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
        }

        // Methods.
        public async Task RunAsync()
        {
            if (!options.RunBzzFunding && !options.RunXdaiFunding)
                return;

            var chestAccount = new Account(options.ChestPrivateKey, options.ChainId);

            // For each node, even if actually offline.
            foreach (var node in liveManager.AllNodes)
            {
                // Bzz funding.
                if (options.RunBzzFunding)
                {
                    // Get node amount.
                    BigInteger nodeAmount = 0;
                    //TODO

                    // Fund node.
                    if (nodeAmount < options.BzzMinTrigger)
                    {
                        BigInteger funded = 0;
                        try
                        {
                            //TODO

                            logger.SuccededToFundBzzOnNode(node.Id, funded, nodeAmount + funded);
                        }
                        catch (Exception ex) //TODO
                        {
                            

                            logger.FailedToFundBzzOnNode(node.Id, funded, ex);
                        }
                    }

                }

                // xDai funding.
                if (options.RunXdaiFunding)
                {
                    // Get node amount.
                    BigInteger nodeAmount = 0;
                    //TODO

                    // Fund node.
                    if (nodeAmount < options.XDaiMinTrigger)
                    {
                        BigInteger funded = 0;
                        try
                        {
                            //TODO

                            logger.SuccededToFundXDaiOnNode(node.Id, funded, nodeAmount + funded);
                        }
                        catch (Exception ex) //TODO
                        {
                            

                            logger.FailedToFundXDaiOnNode(node.Id, funded, ex);
                        }
                    }

                }
            }
        }
    }
}

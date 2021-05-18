using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Utilities;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class CashoutAllNodesTask : ICashoutAllNodesTask
    {
        // Consts.
        public const string TaskId = "cashoutAllNodesTask";

        // Fields.
        private readonly IBeeNodesManager beeNodesManager;
        private readonly IBeehiveContext context;

        // Constructors.
        public CashoutAllNodesTask(
            IBeeNodesManager beeNodesManager,
            IBeehiveContext context)
        {
            this.beeNodesManager = beeNodesManager;
            this.context = context;
        }

        // Methods.
        public async Task RunAsync()
        {
            // List all nodes.
            await context.BeeNodes.Collection.Find(FilterDefinition<BeeNode>.Empty, new FindOptions { NoCursorTimeout = true })
                .ForEachAsync(async node =>
                {
                });
        }
    }
}

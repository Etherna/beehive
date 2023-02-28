using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using System;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Domain
{
    public class BeeNodeService : IBeeNodeService
    {
        // Fields.
        private readonly IBeeNodeLiveManager beeNodeLiveManager;
        private readonly IBeehiveDbContext dbContext;

        // Constructor.
        public BeeNodeService(
            IBeeNodeLiveManager beeNodeLiveManager,
            IBeehiveDbContext dbContext)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
            this.dbContext = dbContext;
        }

        // Methods.
        public async Task<BeeNode> SelectRandomHealthyNodeAsync()
        {
            var instance = await beeNodeLiveManager.TrySelectHealthyNodeAsync(BeeNodeSelectionMode.Random) ??
                throw new InvalidOperationException("Can't select a valid healthy node");
            return await dbContext.BeeNodes.FindOneAsync(instance.Id);
        }
    }
}

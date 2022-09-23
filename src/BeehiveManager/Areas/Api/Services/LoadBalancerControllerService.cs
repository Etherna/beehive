using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Services.Utilities;
using MoreLinq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class LoadBalancerControllerService : ILoadBalancerControllerService
    {
        // Fields.
        private readonly IBeeNodeLiveManager beeNodeLiveManager;
        private readonly IBeehiveDbContext dbContext;

        // Constructor.
        public LoadBalancerControllerService(
            IBeeNodeLiveManager beeNodeLiveManager,
            IBeehiveDbContext dbContext)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
            this.dbContext = dbContext;
        }

        // Methods.
        public async Task<BeeNodeDto> SelectDownloadNodeAsync(string hash)
        {
            // Try to find a pinning node.
            var beeNodeLiveInstances = beeNodeLiveManager.GetBeeNodeLiveInstancesByPinnedContent(hash, true);
            if (beeNodeLiveInstances.Any())
            {
                //select a random one
                var instance = beeNodeLiveInstances.RandomSubset(1).First();
                var node = await dbContext.BeeNodes.FindOneAsync(instance.Id);
                return new BeeNodeDto(node);
            }

            // If there isn't any pinning node, select an alive node.
            beeNodeLiveInstances = beeNodeLiveManager.HealthyNodes;
            if (beeNodeLiveInstances.Any())
            {
                //select a random one
                var instance = beeNodeLiveInstances.RandomSubset(1).First();
                var node = await dbContext.BeeNodes.FindOneAsync(instance.Id);
                return new BeeNodeDto(node);
            }

            // Throw exception because doesn't exist any available node.
            throw new InvalidOperationException("Can't select a valid node");
        }
    }
}

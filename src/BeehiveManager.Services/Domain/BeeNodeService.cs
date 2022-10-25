using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using Nethereum.Util;
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
        public async Task<BeeNode> GetPreferredSocBeeNodeAsync(string socOwnerAddress)
        {
            socOwnerAddress = socOwnerAddress.ConvertToEthereumChecksumAddress();

            // Try to find ether address configuration.
            var etherAddressConfig = await dbContext.EtherAddresses.TryFindOneAsync(a => a.Address == socOwnerAddress);

            // If configuration doesn't exist, create it.
            if (etherAddressConfig is null)
            {
                //select a random healthy node
                var selectedNode = await SelectRandomHealthyNodeAsync();

                //create configuration with selected node as preferred
                etherAddressConfig = new EtherAddress(socOwnerAddress) { PreferredSocNode = selectedNode };
                await dbContext.EtherAddresses.CreateAsync(etherAddressConfig);
            }

            // Else, if there is no preferred soc node, select one random and update config.
            else if (etherAddressConfig.PreferredSocNode is null)
            {
                //select a random healthy node
                var selectedNode = await SelectRandomHealthyNodeAsync();

                //update configuration with selected node as preferred
                etherAddressConfig.PreferredSocNode = selectedNode;
                await dbContext.SaveChangesAsync();
            }

            return etherAddressConfig.PreferredSocNode;
        }

        public async Task<BeeNode> SelectRandomHealthyNodeAsync()
        {
            var instance = await beeNodeLiveManager.TrySelectHealthyNodeAsync(BeeNodeSelectionMode.Random);
            if (instance is null)
                throw new InvalidOperationException("Can't select a valid healthy node");

            return await dbContext.BeeNodes.FindOneAsync(instance.Id);
        }
    }
}

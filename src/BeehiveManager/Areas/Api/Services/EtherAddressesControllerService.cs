using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Nethereum.Util;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class EtherAddressesControllerService : IEtherAddressesControllerService
    {
        // Fields.
        private readonly IBeehiveDbContext dbContext;

        // Constructor.
        public EtherAddressesControllerService(
            IBeehiveDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        // Methods.
        public async Task<EtherAddressDto> FindEtherAddressConfigAsync(string address)
        {
            address = address.ConvertToEthereumChecksumAddress();
            var etherAddressConfig = await dbContext.EtherAddressConfigs.TryFindOneAsync(a => a.Address == address);
            return new EtherAddressDto(etherAddressConfig ?? new EtherAddressConfig(address));
        }

        public async Task SetPreferredSocNodeAsync(string address, string nodeId)
        {
            address = address.ConvertToEthereumChecksumAddress();
            var etherAddressConfig = await dbContext.EtherAddressConfigs.TryFindOneAsync(a => a.Address == address);
            var node = await dbContext.BeeNodes.FindOneAsync(nodeId);

            if (etherAddressConfig is null)
            {
                etherAddressConfig = new EtherAddressConfig(address) { PreferredSocNode = node };
                await dbContext.EtherAddressConfigs.CreateAsync(etherAddressConfig);
            }
            else
            {
                etherAddressConfig.PreferredSocNode = node;
                await dbContext.SaveChangesAsync();
            }
        }
    }
}

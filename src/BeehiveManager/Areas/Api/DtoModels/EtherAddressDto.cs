using Etherna.BeehiveManager.Domain.Models;
using System;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class EtherAddressDto
    {
        public EtherAddressDto(EtherAddressConfig etherAddressConfig)
        {
            if (etherAddressConfig is null)
                throw new ArgumentNullException(nameof(etherAddressConfig));

            Address = etherAddressConfig.Address;
            if (etherAddressConfig.PreferredSocNode is not null)
                PreferredSocNode = new BeeNodeDto(etherAddressConfig.PreferredSocNode);
        }

        public string Address { get; }
        public BeeNodeDto? PreferredSocNode { get; }
    }
}

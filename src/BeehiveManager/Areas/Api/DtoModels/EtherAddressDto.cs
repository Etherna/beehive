using Etherna.BeehiveManager.Domain.Models;
using System;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class EtherAddressDto
    {
        public EtherAddressDto(EtherAddress etherAddress)
        {
            if (etherAddress is null)
                throw new ArgumentNullException(nameof(etherAddress));

            Address = etherAddress.Address;
            if (etherAddress.PreferredSocNode is not null)
                PreferredSocNode = new BeeNodeDto(etherAddress.PreferredSocNode);
        }

        public string Address { get; }
        public BeeNodeDto? PreferredSocNode { get; }
    }
}

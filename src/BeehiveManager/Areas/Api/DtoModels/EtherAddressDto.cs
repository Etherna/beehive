using System;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    [Obsolete("This is a dropped feature")]
    public class EtherAddressDto
    {
        public EtherAddressDto(string address)
        {
            Address = address;
        }

        public string Address { get; }
        public BeeNodeDto? PreferredSocNode => null;
    }
}

using Etherna.BeehiveManager.Areas.Api.DtoModels;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface IEtherAddressesControllerService
    {
        Task<EtherAddressDto> FindEtherAddressConfigAsync(string address);
        Task SetPreferredSocNodeAsync(string address, string nodeId);
    }
}
using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.InputModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface INodesControllerService
    {
        Task<BeeNodeDto> AddBeeNodeAsync(BeeNodeInput input);
        Task<BeeNodeDto> FindByIdAsync(string id);
        Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take);
        Task RemoveBeeNodeAsync(string id);
        Task<BeeNodeDto> RetrieveAddressesAsync(string id);
    }
}
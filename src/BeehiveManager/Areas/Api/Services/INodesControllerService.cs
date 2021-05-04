using Etherna.BeehiveManager.Areas.Api.DtoModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface INodesControllerService
    {
        Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take);
    }
}
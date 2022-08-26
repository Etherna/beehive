using Etherna.BeehiveManager.Areas.Api.DtoModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface IPinningControllerService
    {
        Task<IEnumerable<BeeNodeDto>> FindBeeNodesPinningContentAsync(string hash, bool requireAliveNodes);
        Task<string> PinContentInNodeAsync(string hash, string? nodeId);
    }
}
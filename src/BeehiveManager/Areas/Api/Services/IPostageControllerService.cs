using Etherna.BeehiveManager.Areas.Api.DtoModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface IPostageControllerService
    {
        Task<string> BuyPostageBatchAsync(long amount, int depth, long? gasPrice, bool immutable, string? label, string? nodeId);
        Task<IEnumerable<PostageBatchDto>> GetPostageBatchesFromAllNodes();
    }
}
using Etherna.BeehiveManager.Areas.Api.DtoModels;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface IPostageControllerService
    {
        Task<PostageBatchRefDto> BuyPostageBatchAsync(long amount, int depth, long? gasPrice, bool immutable, string? label, string? nodeId);
        Task<BeeNodeDto> FindBeeNodeOwnerOfPostageBatchAsync(string batchId);
    }
}
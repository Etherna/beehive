using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface IPostageControllerService
    {
        Task<string> BuyPostageBatchAsync(int depth, int plurAmount, int? gasPrice, bool immutable, string? label, string? nodeId);
    }
}
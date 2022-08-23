using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface IPinningControllerService
    {
        Task<string> PinContentInNodeAsync(string hash, string? nodeId);
    }
}
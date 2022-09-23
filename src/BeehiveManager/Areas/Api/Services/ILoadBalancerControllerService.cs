using Etherna.BeehiveManager.Areas.Api.DtoModels;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface ILoadBalancerControllerService
    {
        Task<BeeNodeDto> SelectDownloadNodeAsync(string hash);
    }
}
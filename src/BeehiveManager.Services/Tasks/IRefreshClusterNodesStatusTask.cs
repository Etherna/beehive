using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public interface IRefreshClusterNodesStatusTask
    {
        Task RunAsync();
    }
}
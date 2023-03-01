using Hangfire;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public interface IFundNodesTask
    {
        [Queue(Queues.NODE_MAINTENANCE)]
        Task RunAsync();
    }
}
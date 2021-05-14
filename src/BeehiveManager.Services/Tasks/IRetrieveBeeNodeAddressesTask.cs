using Hangfire;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public interface IRetrieveBeeNodeAddressesTask
    {
        [Queue(Queues.DOMAIN_MAINTENANCE)]
        Task RunAsync(string nodeId);
    }
}
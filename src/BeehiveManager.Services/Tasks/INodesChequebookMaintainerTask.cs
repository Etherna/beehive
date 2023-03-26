using Hangfire;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    /// <summary>
    /// Deposit or withdraw funds from chequebooks of maintained nodes, when they pass min or max limits.
    /// </summary>
    public interface INodesChequebookMaintainerTask
    {
        [Queue(Queues.NODE_MAINTENANCE)]
        Task RunAsync();
    }
}
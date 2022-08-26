using Etherna.BeehiveManager.Services.Utilities;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class PinContentInNodeTask : IPinContentInNodeTask
    {
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        public PinContentInNodeTask(
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        public async Task RunAsync(string contentHash, string nodeId)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);

            // Pin.
            await beeNodeInstance.PinResourceAsync(contentHash);
        }
    }
}

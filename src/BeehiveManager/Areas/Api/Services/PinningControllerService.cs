using Etherna.BeehiveManager.Services.Tasks;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using Hangfire;
using System;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class PinningControllerService : IPinningControllerService
    {
        // Fields.
        private readonly IBackgroundJobClient backgroundJobClient;
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        // Constructor.
        public PinningControllerService(
            IBackgroundJobClient backgroundJobClient,
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.backgroundJobClient = backgroundJobClient;
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        // Methods.
        public async Task<string> PinContentInNodeAsync(string hash, string? nodeId)
        {
            // Try to select an healthy node that doesn't already own the pin, if not specified.
            nodeId ??= (await beeNodeLiveManager.TrySelectHealthyNodeAsync(
                BeeNodeSelectionMode.RoundRobin,
                "pinNewContent",
                async node => !await node.IsPinningResourceAsync(hash)))?.Id;

            if (nodeId is null)
                throw new InvalidOperationException("No healthy nodes available to pin");

            // Schedule task.
            backgroundJobClient.Enqueue<IPinContentInNodeTask>(
                task => task.RunAsync(hash, nodeId));

            return nodeId;
        }
    }
}

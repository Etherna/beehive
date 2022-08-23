using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using System;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class PinningControllerService : IPinningControllerService
    {
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        // Constructor.
        public PinningControllerService(
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        // Methods.
        public async Task<string> PinContentInNodeAsync(string hash, string? nodeId)
        {
            // Try to select an healthy node that doesn't already own the pin.
            var beeNodeInstance = nodeId is null ?
                await beeNodeLiveManager.TrySelectHealthyNodeAsync(BeeNodeSelectionMode.RoundRobin, "pinNewContent", async node => !await node.IsPinningResourceAsync(hash)) :
                await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);

            if (beeNodeInstance is null)
                throw new InvalidOperationException("No healthy nodes available to pin");

            // Pin.
            await beeNodeInstance.PinResourceAsync(hash);

            return beeNodeInstance.Id;
        }
    }
}

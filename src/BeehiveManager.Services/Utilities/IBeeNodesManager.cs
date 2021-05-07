using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeeNet;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Services.Utilities
{
    public interface IBeeNodesManager
    {
        // Properties.
        IReadOnlyDictionary<string, BeeNodeClient> NodeClients { get; }

        // Methods.
        BeeNodeClient GetBeeNodeClient(BeeNode beeNode);
        bool RemoveBeeNodeClient(string id);
    }
}
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeeNet;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Services.Utilities
{
    class BeeNodesManager : IBeeNodesManager
    {
        // Fields.
        private readonly Dictionary<string, BeeNodeClient> _nodeClients = new();

        // Properties.
        public IReadOnlyDictionary<string, BeeNodeClient> NodeClients => _nodeClients;

        // Methods.
        public BeeNodeClient GetBeeNodeClient(BeeNode beeNode)
        {
            if (_nodeClients.ContainsKey(beeNode.Id))
                return _nodeClients[beeNode.Id];

            var client = new BeeNodeClient(beeNode.Url.AbsoluteUri, beeNode.GatewayPort, beeNode.DebugPort);
            _nodeClients.Add(beeNode.Id, client);

            return client;
        }

        public bool RemoveBeeNodeClient(string id) =>
            _nodeClients.Remove(id);
    }
}

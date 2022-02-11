using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeeNet;
using System;

namespace Etherna.BeehiveManager.Services.Utilities
{
    public class BeeNodeStatus
    {
        // Constructor.
        internal BeeNodeStatus(BeeNode beeNode, BeeNodeClient client)
        {
            Id = beeNode.Id;
            Client = client;
            EtherAddress = beeNode.Addresses?.Ethereum;
        }

        // Properties.
        public string Id { get; }
        public BeeNodeClient Client { get; }
        public string? EtherAddress { get; private set; }
        public bool IsAlive { get; set; }

        // Internal methods.
        internal void UpdateInfo(BeeNode node)
        {
            if (Id != node.Id)
                throw new ArgumentException("Node is not the same");

            EtherAddress = node.Addresses?.Ethereum;
        }
    }
}

using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeeNet;
using System;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class BeeNodeLiveInstance
    {
        // Constructor.
        internal BeeNodeLiveInstance(
            BeeNode beeNode)
        {
            Id = beeNode.Id;
            Client = new BeeNodeClient(beeNode.Url.AbsoluteUri, beeNode.GatewayPort, beeNode.DebugPort);
            EtherAddress = beeNode.Addresses?.Ethereum;
            Status = new BeeNodeStatus();
        }

        // Properties.
        public string Id { get; }
        public BeeNodeClient Client { get; }
        public string? EtherAddress { get; private set; }
        public BeeNodeStatus Status { get; }

        // Internal methods.
        internal void UpdateInfo(BeeNode node)
        {
            if (Id != node.Id)
                throw new ArgumentException("Node is not the same");

            EtherAddress = node.Addresses?.Ethereum;
        }
    }
}

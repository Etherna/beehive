using Etherna.BeehiveManager.Domain.Models;
using System;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class BeeNodeDto
    {
        // Constructors.
        public BeeNodeDto(BeeNode beeNode)
        {
            if (beeNode is null)
                throw new ArgumentNullException(nameof(beeNode));

            Id = beeNode.Id;
            DebugPort = beeNode.DebugPort;
            EthereumAddress = beeNode.Addresses?.Ethereum;
            GatewayPort = beeNode.GatewayPort;
            OverlayAddress = beeNode.Addresses?.Overlay;
            PssPublicKey = beeNode.Addresses?.PssPublicKey;
            PublicKey = beeNode.Addresses?.PublicKey;
            Url = beeNode.Url;
        }

        // Properties.
        public string Id { get; }
        public int? DebugPort { get; }
        public string? EthereumAddress { get; }
        public int? GatewayPort { get; }
        public string? OverlayAddress { get; }
        public string? PssPublicKey { get; }
        public string? PublicKey { get; }
        public Uri Url { get; }
    }
}

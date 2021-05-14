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
            EthAddress = beeNode.EthAddress;
            GatewayPort = beeNode.GatewayPort;
            LastRefreshDateTime = beeNode.LastRefreshDateTime;
            Url = beeNode.Url;
        }

        // Properties.
        public string Id { get; }
        public int? DebugPort { get; }
        public string? EthAddress { get; }
        public int? GatewayPort { get; }
        public DateTime? LastRefreshDateTime { get; }
        public Uri Url { get; }
    }
}

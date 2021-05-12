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

            EthAddress = beeNode.EthAddress;
            Id = beeNode.Id;
        }

        // Properties.
        public string? EthAddress { get; }
        public string Id { get; }
    }
}

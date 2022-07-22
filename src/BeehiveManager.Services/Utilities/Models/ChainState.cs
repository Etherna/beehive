using Etherna.BeeNet.DtoModels;
using System;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class ChainState
    {
        public ChainState(string nodeId, ChainStateDto chainStateDto)
        {
            if (chainStateDto is null)
                throw new ArgumentNullException(nameof(chainStateDto));

            Block = chainStateDto.Block;
            CurrentPrice = chainStateDto.CurrentPrice;
            SourceNodeId = nodeId;
            TimeStamp = DateTime.UtcNow;
            TotalAmount = chainStateDto.TotalAmount;
        }

        public long Block { get; }
        public string CurrentPrice { get; }
        public string SourceNodeId { get; }
        public DateTime TimeStamp { get; }
        public string TotalAmount { get; }
    }
}

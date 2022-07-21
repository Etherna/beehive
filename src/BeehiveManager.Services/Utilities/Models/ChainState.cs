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
            ChainTip = chainStateDto.ChainTip;
            CurrentPrice = chainStateDto.CurrentPrice;
            SourceNodeId = nodeId;
            TimeStamp = DateTime.UtcNow;
            TotalAmount = chainStateDto.TotalAmount;
        }

        public int Block { get; }
        public int ChainTip { get; }
        public int CurrentPrice { get; }
        public string SourceNodeId { get; }
        public DateTime TimeStamp { get; }
        public int TotalAmount { get; }
    }
}

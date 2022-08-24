using Etherna.BeehiveManager.Services.Utilities.Models;
using System;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class ChainStateDto
    {
        public ChainStateDto(ChainState chainState)
        {
            if (chainState is null)
                throw new ArgumentNullException(nameof(chainState));

            Block = chainState.Block;
            CurrentPrice = chainState.CurrentPrice;
            SourceNodeId = chainState.SourceNodeId;
            TimeStamp = chainState.TimeStamp;
            TotalAmount = chainState.TotalAmount;
        }

        public long Block { get; }
        public long CurrentPrice { get; }
        public string SourceNodeId { get; }
        public DateTime TimeStamp { get; }
        public long TotalAmount { get; }
    }
}

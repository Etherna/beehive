using Etherna.BeeNet.DtoModels;
using System;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class PostageBatch
    {
        // Constructor.
        public PostageBatch(PostageBatchDto batchDto)
        {
            if (batchDto is null)
                throw new ArgumentNullException(nameof(batchDto));

            Id = batchDto.Id;
            AmountPaid = batchDto.AmountPaid;
            BlockNumber = batchDto.BlockNumber;
            BucketDepth = batchDto.BucketDepth;
            Depth = batchDto.Depth;
            IsImmutable = batchDto.ImmutableFlag;
            Label = batchDto.Label;
        }

        // Properties.
        public string Id { get; }
        public long? AmountPaid { get; }
        public int BlockNumber { get; }
        public int BucketDepth { get; }
        public int Depth { get; }
        public bool IsImmutable { get; }
        public string? Label { get; }
    }
}

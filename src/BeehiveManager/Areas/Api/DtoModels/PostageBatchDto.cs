using System;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class PostageBatchDto
    {
        // Constructors.
        public PostageBatchDto(BeeNet.DtoModels.PostageBatchDto postageBatch)
        {
            if (postageBatch is null)
                throw new ArgumentNullException(nameof(postageBatch));

            Id = postageBatch.Id;
            AmountPaid = postageBatch.AmountPaid;
            BatchTTL = postageBatch.BatchTTL;
            BlockNumber = postageBatch.BlockNumber;
            BucketDepth = postageBatch.BucketDepth;
            Depth = postageBatch.Depth;
            Exists = postageBatch.Exists;
            ImmutableFlag = postageBatch.ImmutableFlag;
            Label = postageBatch.Label;
            OwnerAddress = postageBatch.OwnerAddress;
            Usable = postageBatch.Usable;
            Utilization = postageBatch.Utilization;
        }

        // Properties.
        public string Id { get; }
        public long? AmountPaid { get; }
        public int BatchTTL { get; }
        public int BlockNumber { get; }
        public int BucketDepth { get; }
        public int Depth { get; }
        public bool Exists { get; }
        public bool ImmutableFlag { get; }
        public string? Label { get; }
        public long NormalisedBalance { get; }
        public string? OwnerAddress { get; }
        public bool Usable { get; }
        public int? Utilization { get; }
    }
}

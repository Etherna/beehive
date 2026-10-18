// Copyright 2021-present Etherna SA
// This file is part of Beehive.
// 
// Beehive is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Beehive is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Beehive.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Beehive.Domain.Models;
using Etherna.SwarmSdk.Models;
using System;
using System.Linq;

namespace Etherna.Beehive.Services.Utilities.Models
{
    /// <summary>
    /// What beehive can tell about a postage batch: the live state answered by its owner node or, when the node
    /// could not answer, what the batch cache knows plus the reason
    /// </summary>
    public sealed class PostageBatchResult
    {
        // Constructors.
        public PostageBatchResult(PostageBatch postageBatch)
        {
            ArgumentNullException.ThrowIfNull(postageBatch);

            Amount = postageBatch.Amount;
            BatchId = postageBatch.Id;
            BlockNumber = postageBatch.BlockNumber;
            Depth = postageBatch.Depth;
            Exists = postageBatch.Exists;
            IsImmutable = postageBatch.IsImmutable;
            IsUsable = postageBatch.IsUsable;
            Label = postageBatch.Label;
            Ttl = postageBatch.Ttl;
            Utilization = postageBatch.Utilization;
        }

        public PostageBatchResult(PostageBatchCache postageBatchCache, string error)
        {
            ArgumentNullException.ThrowIfNull(postageBatchCache);
            ArgumentNullException.ThrowIfNull(error);

            BatchId = postageBatchCache.BatchId;
            Depth = postageBatchCache.Depth;
            Error = error;
            IsImmutable = postageBatchCache.IsImmutable;
            Label = "";
            Utilization = postageBatchCache.Buckets.Max();
        }

        // Properties.
        public BzzValue? Amount { get; }
        public PostageBatchId BatchId { get; }
        //unknown in a degraded entry: non-nullable again once beehive manages the postage batches itself
        public ulong? BlockNumber { get; }
        public int? Depth { get; }

        /// <summary>
        /// The reason the owner node could not answer: null when the details are live
        /// </summary>
        public string? Error { get; }

        //unknown in a degraded entry: non-nullable again once beehive manages the postage batches itself
        public bool? Exists { get; }
        public bool IsImmutable { get; }
        public bool IsUsable { get; }
        public string Label { get; }
        //unknown in a degraded entry: non-nullable again once beehive manages the postage batches itself
        public TimeSpan? Ttl { get; }
        public uint Utilization { get; }
    }
}

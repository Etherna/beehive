// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeeNet.Models;
using System;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class PostageBatchDto
    {
        // Constructors.
        public PostageBatchDto(PostageBatch postageBatch)
        {
            ArgumentNullException.ThrowIfNull(postageBatch, nameof(postageBatch));

            Id = postageBatch.Id.ToString();
            Value = postageBatch.Amount.ToPlurLong();
            BatchTTL = (long)postageBatch.Ttl.TotalSeconds;
            BlockNumber = postageBatch.BlockNumber;
            BucketDepth = PostageBatch.BucketDepth;
            Depth = postageBatch.Depth;
            Exists = postageBatch.Exists;
            ImmutableFlag = postageBatch.IsImmutable;
            Label = postageBatch.Label;
            Usable = postageBatch.IsUsable;
            Utilization = postageBatch.Utilization;
        }

        // Properties.
        public string Id { get; }
        public long BatchTTL { get; }
        public int BlockNumber { get; }
        public int BucketDepth { get; }
        public int Depth { get; }
        public bool Exists { get; }
        public bool ImmutableFlag { get; }
        public string? Label { get; }
        public bool Usable { get; }
        public uint? Utilization { get; }
        public long? Value { get; }
    }
}

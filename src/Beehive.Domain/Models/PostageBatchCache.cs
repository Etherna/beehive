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

using Etherna.BeeNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.Beehive.Domain.Models
{
    public class PostageBatchCache : EntityModelBase<string>
    {
        // Fields.
        private uint[] _buckets = new uint[PostageBuckets.BucketsSize];
        
        // Constructors.
        public PostageBatchCache(
            PostageBatchId batchId,
            uint[] buckets,
            int depth,
            bool isImmutable,
            string ownerNodeId)
        {
            ArgumentNullException.ThrowIfNull(buckets, nameof(buckets));
            if (buckets.Length != PostageBuckets.BucketsSize)
                throw new ArgumentOutOfRangeException(nameof(buckets), "Wrong buckets amount");
            
            BatchId = batchId;
            Buckets = buckets;
            Depth = depth;
            IsImmutable = isImmutable;
            OwnerNodeId = ownerNodeId;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected PostageBatchCache() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        
        // Properties.
        public virtual PostageBatchId BatchId { get; protected set; }
        public virtual IEnumerable<uint> Buckets
        {
            get => _buckets;
            protected set => _buckets = value.ToArray();
        }
        public virtual int Depth { get; set; }
        public virtual bool IsImmutable { get; protected set; }
        public virtual string OwnerNodeId { get; protected set; }
    }
}
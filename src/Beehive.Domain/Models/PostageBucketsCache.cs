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
    public class PostageBucketsCache : EntityModelBase<string>
    {
        // Fields.
        private uint[] _bucketsCollisions = new uint[PostageBuckets.BucketsSize];
        
        // Constructors.
        public PostageBucketsCache(
            PostageBatchId batchId,
            uint[] bucketsCollisions,
            uint depth,
            string ownerNodeId)
        {
            ArgumentNullException.ThrowIfNull(bucketsCollisions, nameof(bucketsCollisions));
            if (bucketsCollisions.Length != PostageBuckets.BucketsSize)
                throw new ArgumentOutOfRangeException(nameof(bucketsCollisions), "Wrong buckets amount");
            
            BatchId = batchId;
            BucketsCollisions = bucketsCollisions;
            Depth = depth;
            OwnerNodeId = ownerNodeId;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected PostageBucketsCache() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        
        // Properties.
        public virtual PostageBatchId BatchId { get; protected set; }
        public virtual IEnumerable<uint> BucketsCollisions
        {
            get => _bucketsCollisions;
            protected set => _bucketsCollisions = value.ToArray();
        }
        public virtual uint Depth { get; protected set; }
        public virtual string OwnerNodeId { get; protected set; }
    }
}
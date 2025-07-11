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
using System.Text.Json.Serialization;

namespace Etherna.Beehive.Areas.Api.Bee.DtoModels
{
    public class GlobalPostageBatchDto(
        PostageBatchId batchId,
        BzzBalance amount,
        ulong blockNumber,
        int depth,
        int bucketDepth,
        bool isImmutable,
        TimeSpan ttl,
        EthAddress owner)
    {
        [JsonPropertyName("batchID")]
        public PostageBatchId BatchId { get; } = batchId;
        public BzzBalance Value { get; } = amount;
        public ulong Start { get; } = blockNumber;
        public string Owner { get; } = owner.ToString(false);
        public int Depth { get; } = depth;
        public int BucketDepth { get; } = bucketDepth;
        public bool Immutable { get; } = isImmutable;
        [JsonPropertyName("batchTTL")]
        public TimeSpan BatchTtl { get; } = ttl;
    }
}
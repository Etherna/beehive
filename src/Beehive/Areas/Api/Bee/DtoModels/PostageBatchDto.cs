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
    public sealed class PostageBatchDto(
        BzzBalance amount,
        PostageBatchId batchId,
        TimeSpan batchTtl,
        ulong blockNumber,
        int bucketDepth,
        int depth,
        bool exists,
        bool immutableFlag,
        string label,
        bool usable,
        uint utilization)
    {
        public BzzBalance Amount { get; } = amount;
        [JsonPropertyName("batchID")]
        public PostageBatchId BatchId { get; } = batchId;
        [JsonPropertyName("batchTTL")]
        public TimeSpan BatchTtl { get; } = batchTtl;
        public ulong BlockNumber { get; } = blockNumber;
        public int BucketDepth { get; } = bucketDepth;
        public int Depth { get; } = depth;
        public bool Exists { get; } = exists;
        public bool ImmutableFlag { get; } = immutableFlag;
        public string Label { get; } = label;
        public bool Usable { get; } = usable;
        public uint Utilization { get; } = utilization;
    }
}
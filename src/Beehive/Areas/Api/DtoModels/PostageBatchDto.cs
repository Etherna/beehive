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

namespace Etherna.Beehive.Areas.Api.DtoModels
{
    // Bee output for compatibility:
    // {
    //     "batchID": "42d923558a0fcec12cd6c84ef4118f32caa694d60ae677e1ebe76ad8e470d099",
    //     "utilization": 0,
    //     "usable": true,
    //     "label": "",
    //     "depth": 17,
    //     "amount": "2831120640",
    //     "bucketDepth": 16,
    //     "blockNumber": 44667643,
    //     "immutableFlag": false,
    //     "exists": true,
    //     "batchTTL": 120105
    // }
    public sealed class PostageBatchDto(
        BzzValue? amount,
        PostageBatchId batchId,
        TimeSpan batchTtl,
        ulong blockNumber,
        int bucketDepth,
        int? depth,
        bool exists,
        bool immutableFlag,
        string label,
        bool usable,
        uint utilization)
    {
        [JsonPropertyName("batchID")]
        public PostageBatchId BatchId { get; } = batchId;
        public uint Utilization { get; } = utilization;
        public bool Usable { get; } = usable;
        public string Label { get; } = label;
        public int? Depth { get; } = depth;
        public BzzValue? Amount { get; } = amount;
        public int BucketDepth { get; } = bucketDepth;
        public ulong BlockNumber { get; } = blockNumber;
        public bool ImmutableFlag { get; } = immutableFlag;
        public bool Exists { get; } = exists;
        [JsonPropertyName("batchTTL")]
        public TimeSpan BatchTtl { get; } = batchTtl;
    }
}
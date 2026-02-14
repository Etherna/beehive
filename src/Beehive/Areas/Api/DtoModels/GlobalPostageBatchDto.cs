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
    //     "batches": [
    //         {
    //             "batchID": "000f38970301f91fea58da8460dbe6597d216e277741683bd66545eacadbbfc4",
    //             "value": "567151591686",
    //             "start": 44670592,
    //             "owner": "7f2f2d4c043714cd6f9af2c6da3cb7fd3165e84d",
    //             "depth": 19,
    //             "bucketDepth": 16,
    //             "immutable": true,
    //             "batchTTL": 75830
    //         }
    //     ]
    // }
    public sealed class GlobalPostageBatchDto(
        PostageBatchId batchId,
        BzzValue? value,
        ulong blockNumber,
        int? depth,
        int bucketDepth,
        bool isImmutable,
        TimeSpan ttl,
        EthAddress owner)
    {
        [JsonPropertyName("batchID")]
        public PostageBatchId BatchId { get; } = batchId;
        public BzzValue? Value { get; } = value;
        public ulong Start { get; } = blockNumber;
        public string Owner { get; } = owner.ToString(false);
        public int? Depth { get; } = depth;
        public int BucketDepth { get; } = bucketDepth;
        public bool Immutable { get; } = isImmutable;
        [JsonPropertyName("batchTTL")]
        public TimeSpan BatchTtl { get; } = ttl;
    }
}
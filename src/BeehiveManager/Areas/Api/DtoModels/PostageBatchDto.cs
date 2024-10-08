// Copyright 2021-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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

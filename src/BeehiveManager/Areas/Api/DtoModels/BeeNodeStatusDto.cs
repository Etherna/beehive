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

using Etherna.BeehiveManager.Services.Utilities.Models;
using System;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class BeeNodeStatusDto
    {
        // Constructor.
        public BeeNodeStatusDto(string id, BeeNodeStatus status)
        {
            if (status is null)
                throw new ArgumentNullException(nameof(status));

            Id = id;
            Errors = status.Errors;
            EthereumAddress = status.Addresses?.Ethereum;
            HeartbeatTimeStamp = status.HeartbeatTimeStamp;
            IsAlive = status.IsAlive;
            OverlayAddress = status.Addresses?.Overlay;
            PinnedHashes = status.PinnedHashes;
            PostageBatchesId = status.PostageBatchesId;
            PssPublicKey = status.Addresses?.PssPublicKey;
            PublicKey = status.Addresses?.PublicKey;
        }

        // Properties.
        public string Id { get; }
        public IEnumerable<string> Errors { get; }
        public string? EthereumAddress { get; }
        public DateTime HeartbeatTimeStamp { get; }
        public bool IsAlive { get; }
        public string? OverlayAddress { get; }
        public IEnumerable<string> PinnedHashes { get; }
        public IEnumerable<string> PostageBatchesId { get; }
        public string? PssPublicKey { get; }
        public string? PublicKey { get; }
    }
}

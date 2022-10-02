﻿//   Copyright 2021-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Status comparison is not a required function")]
    public struct BeeNodeStatus
    {
        // Constructor.
        public BeeNodeStatus(
            BeeNodeAddresses? addresses,
            IEnumerable<string>? errors,
            DateTime heartbeatTimeStamp,
            bool isAlive,
            IEnumerable<string>? pinnedHashes,
            IEnumerable<string>? postageBatchesId)
        {
            Addresses = addresses;
            Errors = errors;
            HeartbeatTimeStamp = heartbeatTimeStamp;
            IsAlive = isAlive;
            PinnedHashes = pinnedHashes;
            PostageBatchesId = postageBatchesId;
        }

        // Properties.
        public BeeNodeAddresses? Addresses { get; }
        public IEnumerable<string>? Errors { get; }
        public DateTime HeartbeatTimeStamp { get; }
        public bool IsAlive { get; }
        public IEnumerable<string>? PinnedHashes { get; }
        public IEnumerable<string>? PostageBatchesId { get; }
    }
}

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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class BeeNodeStatus
    {
        // Fields.
        private readonly List<string> _errors = new();
        private readonly HashSet<string> _pinnedHashes = new();
        private readonly HashSet<string> _postageBatchesId = new();

        // Constructor.
        public BeeNodeStatus()
        {
            RequireFullRefresh = true;
        }

        // Properties.
        public BeeNodeAddresses? Addresses { get; private set; }
        public IEnumerable<string> Errors => _errors;
        public DateTime HeartbeatTimeStamp { get; private set; }
        public bool IsAlive { get; private set; }
        public IEnumerable<string> PinnedHashes => _pinnedHashes;
        public IEnumerable<string> PostageBatchesId => _postageBatchesId;
        public bool RequireFullRefresh { get; private set; }

        // Internal methods.
        internal void AddPinnedHash(string hash)
        {
            lock (_pinnedHashes)
                _pinnedHashes.Add(hash);
        }

        internal void AddPostageBatchId(string batchId)
        {
            lock (_postageBatchesId)
                _postageBatchesId.Add(batchId);
        }

        internal void FailedHeartbeatAttempt(IEnumerable<string> errors, DateTime timestamp)
        {
            lock (_errors)
            {
                _errors.Clear();
                _errors.AddRange(errors);
            }
            HeartbeatTimeStamp = timestamp;
            IsAlive = false;
        }

        internal void InitializeAddresses(BeeNodeAddresses addresses)
        {
            if (Addresses is not null)
                throw new InvalidOperationException();
            Addresses = addresses;
        }

        internal void RemovePinnedHash(string hash)
        {
            lock (_pinnedHashes)
                _pinnedHashes.Remove(hash);
        }

        internal void SucceededHeartbeatAttempt(
            IEnumerable<string> errors,
            DateTime timestamp,
            IEnumerable<string>? refreshedPinnedHashes,
            IEnumerable<string>? refreshedPostageBatchesId)
        {
            lock (_errors)
            {
                _errors.Clear();
                _errors.AddRange(errors);
            }
            HeartbeatTimeStamp = timestamp;
            IsAlive = true;

            if (refreshedPinnedHashes is not null)
            {
                lock (_pinnedHashes)
                {
                    _pinnedHashes.Clear();
                    foreach (var hash in refreshedPinnedHashes)
                        _pinnedHashes.Add(hash);
                }
            }

            if (refreshedPostageBatchesId is not null)
            {
                lock (_postageBatchesId)
                {
                    /* Don't clear, because postage batches just created on node could not appear from the node query.
                     * Because of this, if we created a new postage, and we try to refresh with only info coming from node,
                     * the postage Id reference could be lost in status.
                     * Adding instead never remove a postage batch id. This is fine, because an owned postage batch can't be removed
                     * by node's logic. It only can expire, but this is not concern of this part of code. */
                    foreach (var batchId in refreshedPostageBatchesId)
                        _postageBatchesId.Add(batchId);
                }
            }

            RequireFullRefresh &= errors.Any() ||
                refreshedPinnedHashes is null ||
                refreshedPostageBatchesId is null;
        }
    }
}

// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeeNet.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class BeeNodeStatus
    {
        // Fields.
        private readonly List<string> _errors = new();
        private readonly HashSet<SwarmHash> _pinnedHashes = new();
        private readonly HashSet<PostageBatchId> _postageBatchesId = new();

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
        public IEnumerable<SwarmHash> PinnedHashes => _pinnedHashes;
        public IEnumerable<PostageBatchId> PostageBatchesId => _postageBatchesId;
        public bool RequireFullRefresh { get; private set; }

        // Internal methods.
        internal void AddPinnedHash(SwarmHash hash)
        {
            lock (_pinnedHashes)
                _pinnedHashes.Add(hash);
        }

        internal void AddPostageBatchId(PostageBatchId batchId)
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

        internal void RemovePinnedHash(SwarmHash hash)
        {
            lock (_pinnedHashes)
                _pinnedHashes.Remove(hash);
        }

        internal void SucceededHeartbeatAttempt(
            IEnumerable<string> errors,
            DateTime timestamp,
            IEnumerable<SwarmHash>? refreshedPinnedHashes,
            IEnumerable<PostageBatchId>? refreshedPostageBatchesId)
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

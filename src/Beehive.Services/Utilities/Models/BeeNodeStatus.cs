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

using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Services.Utilities.Models
{
    public class BeeNodeStatus
    {
        // Fields.
        private readonly List<string> _errors = new();

        // Properties.
        public BeeNodeAddresses? Addresses { get; private set; }
        public IEnumerable<string> Errors => _errors;
        public DateTime HeartbeatTimeStamp { get; private set; }
        public bool IsAlive { get; private set; }

        /// <summary>
        /// The reason the node can't be asked, from its last heartbeat: null while it is alive
        /// </summary>
        public string? NotAliveReason
        {
            get
            {
                if (IsAlive)
                    return null;
                lock (_errors)
                {
                    return _errors.Count > 0 ? string.Join("; ", _errors) : "Node is not alive";
                }
            }
        }

        // Internal methods.
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

        internal void SucceededHeartbeatAttempt(DateTime timestamp)
        {
            lock (_errors)
            {
                _errors.Clear();
            }
            HeartbeatTimeStamp = timestamp;
            IsAlive = true;
        }
    }
}

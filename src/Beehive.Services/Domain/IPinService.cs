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

using Etherna.Beehive.Services.Utilities.Models;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public interface IPinService
    {
        Task<IResourceLockLease> AcquireLockAsync(
            string chunkPinId,
            bool exclusiveAccess);

        Task<bool> IsLockedAsync(string chunkPinId);

        /// <summary>
        /// Pin the chunks of a pin's reference, under the pin's exclusive lock. The chunks beehive doesn't hold are
        /// read from the source node, or from the healthy nodes when none is given, and saved with the pin attached;
        /// the pin is succeeded only when every chunk met carries it on the db. A cancellation releases the lock
        /// and leaves the pin not processed
        /// </summary>
        /// <returns>The pin, processed: succeeded, or with its missing chunks; and the invalid chunks met</returns>
        Task<PinChunksResult> PinChunksAsync(
            string chunkPinId,
            BeeNodeLiveInstance? sourceNode,
            CancellationToken cancellationToken = default);
        
        Task<bool> TryDeletePinAsync(SwarmReference pinReference);
    }
}
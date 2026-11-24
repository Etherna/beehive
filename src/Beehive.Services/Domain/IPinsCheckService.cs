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

using Etherna.Beehive.Domain.Models;
using Etherna.SwarmSdk.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    /// <summary>
    /// The integrity check of beehive's own pins as a background job, with its runs and their pin records
    /// </summary>
    public interface IPinsCheckService
    {
        /// <summary>
        /// Delete the finished checks, with their pin records: the failed ones, the others (completed or stopped),
        /// or both. A running check is never deleted
        /// </summary>
        /// <param name="failed">True for the failed checks only, false for the completed and the stopped ones only, null for both</param>
        /// <returns>The deleted checks</returns>
        Task<long> DeleteChecksAsync(bool? failed, CancellationToken cancellationToken = default);

        /// <summary>
        /// A check, by its id. A check whose job is gone without completing it is failed
        /// </summary>
        Task<PinsCheck> GetCheckAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// The checks, newest first. A check whose job is gone without completing it is failed
        /// </summary>
        Task<IReadOnlyList<PinsCheck>> GetChecksAsync(
            int page,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Start a check of the pins, as a background job: of the requested references, or of the pins of the
        /// requested states
        /// </summary>
        /// <param name="pinStates">The states of the pins to check, when no reference is requested: null or empty for the pins of every state</param>
        /// <param name="references">The contents to check: null or empty for the pins chosen by their state</param>
        /// <exception cref="System.ArgumentException">Both some states and some references are given, or a state that is none of a pin</exception>
        /// <exception cref="Etherna.Beehive.Domain.Exceptions.DeprecatedChunksNotMigratedException">A deprecated chunk store still holds chunks: the pins are checked on the chunks keyed by hash only</exception>
        /// <exception cref="Etherna.Beehive.Domain.Exceptions.ResourceLockException">A check is still running</exception>
        Task<PinsCheck> StartCheckAsync(
            IEnumerable<ChunkPinState>? pinStates = null,
            IEnumerable<SwarmReference>? references = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Ask a check to stop: its run stops at the end of the pin it is checking. A check already ended is left
        /// as it is
        /// </summary>
        Task<PinsCheck> StopCheckAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// The running or last check: null when none was ever started
        /// </summary>
        Task<PinsCheck?> TryGetLatestCheckAsync(CancellationToken cancellationToken = default);
    }
}

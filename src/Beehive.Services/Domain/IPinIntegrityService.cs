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
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public interface IPinIntegrityService
    {
        /// <summary>
        /// Check whether beehive holds the content of a pinned reference, once for every pin of it (a content pinned
        /// by more than one upload has more than one), reading its own chunks keyed by hash only, never the nodes
        /// nor the deprecated stores. A chunk held without one of the pins of the reference among its references
        /// takes it, and counts as held. A chunk that is not a well formed node of the content's tree is counted
        /// invalid, and the check stops at it
        /// </summary>
        /// <param name="dryRun">Whether the check writes nothing: a chunk held without one of the pins among its references counts as held all the same, and is left without it</param>
        /// <returns>The chunks of the content, the missing and the invalid ones</returns>
        Task<PinCheckResult> CheckPinsAsync(
            SwarmReference reference,
            bool dryRun,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bring a pin to what the check of its content found, and write nothing else on it. One found incomplete is reset
        /// (<see cref="ResetPinAsync"/>); one found whole and declared not succeeded is declared succeeded, with the
        /// chunks of its content, under the pin's exclusive lock; one found whole and declared succeeded is as it
        /// says, and is not touched
        /// </summary>
        /// <returns>The pin, as its check found it</returns>
        /// <exception cref="Etherna.Beehive.Domain.Exceptions.ResourceLockException">The pin is locked by another operation</exception>
        Task<ChunkPin> CorrectPinAsync(
            ChunkPin pin,
            PinCheckResult checkResult,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Bring a pin its check found incomplete back to not succeeded, with the missing and the invalid chunks as
        /// its missing ones, under the pin's exclusive lock. The documents of the invalid chunks are deleted, so that
        /// a new pinning reads them again from the nodes, but the malformed one, which a node would serve the same
        /// </summary>
        /// <returns>The pin, not succeeded</returns>
        /// <exception cref="Etherna.Beehive.Domain.Exceptions.ResourceLockException">The pin is locked by another operation</exception>
        Task<ChunkPin> ResetPinAsync(
            string chunkPinId,
            PinCheckResult checkResult,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Take the lock of a check of many pins: one runs at a time, whichever way it runs, as the stream of a
        /// check of every pin or as a background run
        /// </summary>
        /// <returns>The lease of the lock, null while another check of many pins holds it</returns>
        Task<IResourceLockLease?> TryAcquirePinsCheckLockAsync();

        /// <summary>
        /// Verify that the pins can be checked: a check reads the chunks keyed by hash only, so not while the
        /// deprecated chunks collection or the deprecated GridFS bucket still hold a chunk, which it would count
        /// missing while beehive holds it
        /// </summary>
        /// <exception cref="Etherna.Beehive.Domain.Exceptions.DeprecatedChunksNotMigratedException">A deprecated store holds chunks</exception>
        Task VerifyPinsCanBeCheckedAsync(CancellationToken cancellationToken = default);
    }
}

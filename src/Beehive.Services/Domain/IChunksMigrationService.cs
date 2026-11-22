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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    /// <summary>
    /// The migration of the chunks from the deprecated stores to the chunks keyed by hash, run by the operator
    /// </summary>
    public interface IChunksMigrationService
    {
        /// <summary>
        /// The chunks still in the deprecated stores, as estimated by the db
        /// </summary>
        Task<DeprecatedChunksCount> CountDeprecatedChunksAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// The migrations, newest first. A migration whose job is gone without completing it is failed
        /// </summary>
        Task<IReadOnlyList<ChunksMigration>> GetMigrationsAsync(
            int page,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Start a migration, as a background job
        /// </summary>
        /// <exception cref="Etherna.Beehive.Domain.Exceptions.ResourceLockException">A migration is still running</exception>
        Task<ChunksMigration> StartMigrationAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Ask a migration to stop: its run stops at the end of the page it is moving. A migration already ended
        /// is left as it is
        /// </summary>
        Task<ChunksMigration> StopMigrationAsync(string id, CancellationToken cancellationToken = default);

        /// <summary>
        /// The running or last migration: null when none was ever started
        /// </summary>
        Task<ChunksMigration?> TryGetLatestMigrationAsync(CancellationToken cancellationToken = default);
    }
}

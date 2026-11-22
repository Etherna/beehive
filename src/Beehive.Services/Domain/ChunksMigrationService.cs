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

using Etherna.Beehive.Domain.Exceptions;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Tasks.Trigger;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Extensions;
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public sealed class ChunksMigrationService(
        IBackgroundJobClient backgroundJobClient,
        IBeehiveDbContext dbContext,
        IDeprecatedChunkStores deprecatedChunkStores,
        IHangfireJobsMonitor hangfireJobsMonitor,
        ILogger<ChunksMigrationService> logger)
        : IChunksMigrationService
    {
        // Methods.
        public async Task<DeprecatedChunksCount> CountDeprecatedChunksAsync(CancellationToken cancellationToken = default) =>
            new(await deprecatedChunkStores.EstimateLegacyChunksAsync(cancellationToken),
                await deprecatedChunkStores.EstimateGridFsFilesAsync(cancellationToken));

        public async Task<IReadOnlyList<ChunksMigration>> GetMigrationsAsync(
            int page,
            int take,
            CancellationToken cancellationToken = default)
        {
            var migrations = await dbContext.ChunksMigrations.QueryElementsAsync(elements =>
                elements.PaginateDescending(m => m.CreationDateTime, page, take)
                    .ToListAsync(cancellationToken));

            foreach (var migration in migrations)
                await FailIfJobIsGoneAsync(migration);

            return migrations;
        }

        public async Task<ChunksMigration> StartMigrationAsync(CancellationToken cancellationToken = default)
        {
            // One migration at a time.
            var latestMigration = await TryGetLatestMigrationAsync(cancellationToken);
            if (latestMigration is not null && !latestMigration.IsCompleted)
                throw new ResourceLockException("A chunks migration is still running");

            // Record the migration, then run it in background. Once recorded, the writes complete whatever the
            // request does: a migration without its job id would never be found gone.
            var migration = new ChunksMigration();
            await dbContext.ChunksMigrations.CreateAsync(migration, CancellationToken.None);

            var jobId = backgroundJobClient.Enqueue<IMigrateChunksTask>(t => t.RunAsync(migration.Id, CancellationToken.None));
            migration.SetJobId(jobId);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            return migration;
        }

        public async Task<ChunksMigration> StopMigrationAsync(string id, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(id);

            var migration = await dbContext.ChunksMigrations.FindOneAsync(id, cancellationToken);
            await FailIfJobIsGoneAsync(migration);
            if (migration.IsCompleted)
                return migration;

            migration.RequestStop();
            await dbContext.SaveChangesAsync(CancellationToken.None);

            return migration;
        }

        public async Task<ChunksMigration?> TryGetLatestMigrationAsync(CancellationToken cancellationToken = default)
        {
            var migration = (await dbContext.ChunksMigrations.QueryElementsAsync(elements =>
                elements.OrderByDescending(m => m.CreationDateTime)
                    .Take(1)
                    .ToListAsync(cancellationToken))).FirstOrDefault();
            if (migration is null)
                return null;

            await FailIfJobIsGoneAsync(migration);

            return migration;
        }

        // Helpers.
        /// <summary>
        /// A migration whose job is gone without completing it (a server stopped mid-way) will never complete: fail
        /// it. What it moved is out of the deprecated stores, a new migration goes on with the rest
        /// </summary>
        private async Task FailIfJobIsGoneAsync(ChunksMigration migration)
        {
            if (migration.IsCompleted || migration.JobId is null || hangfireJobsMonitor.IsJobActive(migration.JobId))
                return;

            migration.Fail("The migration job ended before completing the migration");
            await dbContext.SaveChangesAsync();
            logger.ChunksMigrationJobGone(migration.Id, migration.JobId);
        }
    }
}

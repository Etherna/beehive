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
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Bson;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Trigger
{
    /// <summary>
    /// Move the chunks of the deprecated stores to the chunks keyed by hash, a page at a time: first the documents
    /// of the deprecated chunks collection, with their pins, then the files of the GridFS bucket. A page is saved as
    /// any chunks (a chunk held already takes the pins of the moved one), and then deleted from its store: the
    /// stores only shrink, so a new run goes on with what is left. Every chunk is verified before it is moved: its
    /// payload must be the chunk of its hash. A chunk that can't be read, or doesn't verify, is logged, counted
    /// and left in its store
    /// </summary>
    public class MigrateChunksTask(
        IBeehiveDbContext dbContext,
        IDeprecatedChunkStores deprecatedChunkStores,
        ILogger<MigrateChunksTask> logger)
        : IMigrateChunksTask
    {
        // Consts.
        /// <summary>
        /// The one resource every run of the migration leases: a single run at a time moves the chunks
        /// </summary>
        public const string LockResourceId = "all";

        // Properties.
        /// <summary>
        /// How long a run waits before asking again for the lease held by another run
        /// </summary>
        internal TimeSpan LeaseRetryPeriod { get; init; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The chunks read, saved and deleted at once
        /// </summary>
        internal int PageSize { get; init; } = 1000;

        // Methods.
        public async Task RunAsync(string chunksMigrationId, CancellationToken cancellationToken)
        {
            // One run at a time moves the chunks, under the exclusive lease. Hangfire can hand the job to a second
            // worker while the first is still performing it, and cancels the first only afterwards: the new run
            // waits for the other one to leave, or for its lease to expire when it is dead.
            IResourceLockLease? acquiredLease;
            var isWaiting = false;
            while ((acquiredLease = await dbContext.TryAcquireResourceLockAsync(
                       ResourceLockNamespaces.ChunksMigrations,
                       LockResourceId)) is null)
            {
                if (!isWaiting)
                    logger.ChunksMigrationHeldByAnotherRun(chunksMigrationId);
                isWaiting = true;
                await Task.Delay(LeaseRetryPeriod, cancellationToken);
            }
            await using var migrationLease = acquiredLease;
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                migrationLease.LeaseLostToken);
            var runToken = runCancellation.Token;

            var migration = await dbContext.ChunksMigrations.FindOneAsync(chunksMigrationId, runToken);
            if (migration.IsCompleted) //a re-run of the job
                return;

            migration.Start(
                await deprecatedChunkStores.EstimateLegacyChunksAsync(runToken),
                await deprecatedChunkStores.EstimateGridFsFilesAsync(runToken));
            await dbContext.SaveChangesAsync(runToken);
            logger.ChunksMigrationStarted(migration.Id, migration.LegacyChunksTotal!.Value, migration.GridFsFilesTotal!.Value);

            try
            {
                if (await MigrateLegacyChunksAsync(migration, runToken) &&
                    await MigrateGridFsFilesAsync(migration, runToken))
                    migration.Complete();
                else
                    migration.StopOnRequest();
            }
            catch (OperationCanceledException) when (migrationLease.LeaseLostToken.IsCancellationRequested)
            {
                // The lease is lost: the migration is not this run's to write anymore.
                logger.ChunksMigrationLeaseLost(migration.Id);
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The job is cancelled, by a shutdown or because another worker took it: the migration stays
                // running, and the next run of the job starts it again.
                logger.ChunksMigrationInterrupted(migration.Id);
                await dbContext.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception e)
            {
                migration.Fail(e.Message);
                await dbContext.SaveChangesAsync(CancellationToken.None);
                logger.ChunksMigrationFailed(migration.Id, e);
                throw;
            }

            await dbContext.SaveChangesAsync(CancellationToken.None);
            if (migration.IsStopped)
                logger.ChunksMigrationStopped(migration.Id);
            else
                logger.ChunksMigrationCompleted(
                    migration.Id,
                    migration.LegacyChunksInserted,
                    migration.LegacyChunksMerged,
                    migration.GridFsFilesInserted,
                    migration.GridFsFilesMerged,
                    migration.LegacyChunksFailed + migration.GridFsFilesFailed);
        }

        // Helpers.
        /// <summary>
        /// Verify that a payload is the chunk of its hash, of the kind it is stored as: a content addressed chunk
        /// hashes to it, a single owner chunk carries the identifier and the signature of the owner that give it
        /// </summary>
        /// <returns>Why the payload is not that chunk, null when it is</returns>
        private static string? FindIntegrityFailure(SwarmHash hash, ReadOnlyMemory<byte> payload, bool isSoc)
        {
            if (IsChunkOfKind(hash, payload, isSoc))
                return null;

            // Tell a chunk stored as the wrong kind from a payload that is no chunk of the hash.
            var kind = isSoc ? "single owner" : "content addressed";
            var otherKind = isSoc ? "content addressed" : "single owner";
            return IsChunkOfKind(hash, payload, !isSoc) ?
                $"its payload is the {otherKind} chunk of {hash}, but it is stored as a {kind} one" :
                $"its payload is not the {kind} chunk of {hash}";
        }

        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A payload that fails to parse is not the chunk")]
        private static bool IsChunkOfKind(SwarmHash hash, ReadOnlyMemory<byte> payload, bool isSoc)
        {
            var chunkBmt = new SwarmChunkBmt();
            if (!isSoc)
                return SwarmCac.IsValid(hash, payload, chunkBmt);

            try
            {
                return SwarmSoc.BuildFromBytes(hash, payload, chunkBmt).ValidateSoc(chunkBmt.Hasher);
            }
            catch (Exception)
            {
                return false; //not the shape, the signature or the address of the chunk
            }
        }

        private async Task<bool> IsStopRequestedAsync(string migrationId, CancellationToken cancellationToken)
        {
            var stopRequests = await dbContext.ChunksMigrations.QueryElementsAsync(elements =>
                elements.Where(m => m.Id == migrationId)
                    .Select(m => m.StopRequestedDateTime)
                    .ToListAsync(cancellationToken));
            return stopRequests.Any(r => r.HasValue);
        }

        /// <summary>
        /// Move the files of the GridFS bucket, a page at a time
        /// </summary>
        /// <returns>True when the bucket was read to its end, false when a stop was asked</returns>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A file failing to move must not stop the others")]
        private async Task<bool> MigrateGridFsFilesAsync(ChunksMigration migration, CancellationToken cancellationToken)
        {
            ObjectId? afterId = null;
            while (true)
            {
                if (await IsStopRequestedAsync(migration.Id, cancellationToken))
                    return false;

                var files = await deprecatedChunkStores.ReadGridFsFilesAsync(afterId, PageSize, cancellationToken);
                if (files.Count == 0)
                    return true;
                afterId = files[^1].Id;

                // Read the chunk of each file: its name is the hash, its content the payload of a content addressed
                // chunk. A file that can't be read, or whose content is not that chunk, stays in the bucket.
                List<(GridFsChunkFile File, Chunk Chunk)> readFiles = [];
                var failed = 0;
                foreach (var file in files)
                {
                    string? failure;
                    try
                    {
                        var hash = SwarmHash.FromString(file.FileName);
                        var payload = await deprecatedChunkStores.DownloadGridFsFileAsync(file.Id, cancellationToken);
                        failure = FindIntegrityFailure(hash, payload, false);
                        if (failure is null)
                            readFiles.Add((file, Chunk.FromDeprecatedStore(hash, payload, false, file.UploadDateTime, [])));
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        failure = $"{e.GetType().Name}: {e.Message}";
                    }

                    if (failure is not null)
                    {
                        logger.GridFsChunkFileMigrationFailed(file.Id.ToString(), file.FileName, failure);
                        failed++;
                    }
                }

                // Save the chunks, then delete their files. The saved chunks leave the db context with the page.
                var merged = 0;
                using (dbContext.StartTransientModelsScope())
                {
                    if (readFiles.Count > 0)
                        merged = await dbContext.SwarmChunks.CreateOrAddPinsAsync(readFiles.Select(f => f.Chunk).ToArray(), cancellationToken);
                    foreach (var (file, _) in readFiles)
                        await deprecatedChunkStores.DeleteGridFsFileAsync(file.Id, cancellationToken);
                }

                migration.GridFsFilesMigrated(files.Count, readFiles.Count - merged, merged, failed);
                await dbContext.SaveChangesAsync(cancellationToken); //the progress is readable while the migration runs
            }
        }

        /// <summary>
        /// Move the documents of the deprecated chunks collection, a page at a time: save the chunks of a page with
        /// their pins, and delete their documents. A document whose pins changed meanwhile is read and moved again,
        /// so a pin added by a concurrent pinning reaches the moved chunk. A document that can't be read, or whose
        /// payload is not the chunk of its hash, stays in the collection
        /// </summary>
        /// <returns>True when the collection was read to its end, false when a stop was asked</returns>
        private async Task<bool> MigrateLegacyChunksAsync(ChunksMigration migration, CancellationToken cancellationToken)
        {
            string? afterId = null;
            while (true)
            {
                if (await IsStopRequestedAsync(migration.Id, cancellationToken))
                    return false;

                var documents = await deprecatedChunkStores.ReadLegacyChunksAsync(afterId, PageSize, cancellationToken);
                if (documents.Count == 0)
                    return true;
                afterId = documents[^1].Id;

                int? merged = null;
                var failed = 0;
                var movingDocuments = documents;
                while (movingDocuments.Count > 0)
                {
                    // Verify the chunks in parallel: hashing a payload is the cpu cost of the move.
                    var failures = movingDocuments.AsParallel().AsOrdered().WithCancellation(cancellationToken)
                        .Select(d => d.Chunk is null ? d.ReadFailure :
                            FindIntegrityFailure(d.Chunk.Hash, d.Chunk.Payload, d.Chunk.IsSoc))
                        .ToArray();

                    List<LegacyChunk> legacyChunks = [];
                    for (var i = 0; i < movingDocuments.Count; i++)
                    {
                        if (failures[i] is null)
                            legacyChunks.Add(movingDocuments[i].Chunk!);
                        else
                        {
                            logger.LegacyChunkMigrationFailed(movingDocuments[i].Id, failures[i]!);
                            failed++;
                        }
                    }
                    if (legacyChunks.Count == 0)
                        break;

                    // The saved chunks leave the db context with the page.
                    IReadOnlyCollection<string> changedIds;
                    using (dbContext.StartTransientModelsScope())
                    {
                        var heldChunks = await dbContext.SwarmChunks.CreateOrAddPinsAsync(
                            legacyChunks.Select(c => Chunk.FromDeprecatedStore(c.Hash, c.Payload, c.IsSoc, c.CreationDateTime, c.Pins)).ToArray(),
                            cancellationToken);
                        merged ??= heldChunks; //a document moved again is held by its first move, not counted twice

                        changedIds = await deprecatedChunkStores.DeleteLegacyChunksAsync(legacyChunks, cancellationToken);
                    }
                    movingDocuments = changedIds.Count == 0 ? [] :
                        await deprecatedChunkStores.ReadLegacyChunksAsync(changedIds, cancellationToken);
                }

                migration.LegacyChunksMigrated(documents.Count, documents.Count - (merged ?? 0) - failed, merged ?? 0, failed);
                await dbContext.SaveChangesAsync(cancellationToken); //the progress is readable while the migration runs
            }
        }
    }
}

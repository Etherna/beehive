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

namespace Etherna.Beehive.Domain.Models
{
    /// <summary>
    /// A run of the migration of the chunks from the deprecated stores to the chunks keyed by hash: first the
    /// documents of the deprecated chunks collection, then the files of the deprecated GridFS bucket. A migrated
    /// chunk leaves its deprecated store, so a new run goes on with what the previous ones left
    /// </summary>
    public class ChunksMigration : EntityModelBase<string>
    {
        // Properties.
        public virtual DateTime? CompletedDateTime { get; protected set; }

        /// <summary>
        /// The reason the run stopped before its end: null while it runs, when it completed, and when it was stopped
        /// </summary>
        public virtual string? FailureReason { get; protected set; }

        /// <summary>
        /// The GridFS files that could not be migrated, left in the bucket
        /// </summary>
        public virtual long GridFsFilesFailed { get; protected set; }

        /// <summary>
        /// The GridFS files moved as new chunks
        /// </summary>
        public virtual long GridFsFilesInserted { get; protected set; }

        /// <summary>
        /// The GridFS files of a chunk held already, deleted without writing it again
        /// </summary>
        public virtual long GridFsFilesMerged { get; protected set; }

        public virtual long GridFsFilesRead { get; protected set; }

        /// <summary>
        /// The GridFS files in the bucket when the run started, as estimated by the db: null until it starts
        /// </summary>
        public virtual long? GridFsFilesTotal { get; protected set; }

        public virtual bool IsCompleted => CompletedDateTime.HasValue;

        /// <summary>
        /// Whether the run ended on a stop request, before migrating everything
        /// </summary>
        public virtual bool IsStopped { get; protected set; }

        /// <summary>
        /// The hangfire job running the migration
        /// </summary>
        public virtual string? JobId { get; protected set; }

        /// <summary>
        /// The deprecated chunk documents that could not be migrated, left in their collection
        /// </summary>
        public virtual long LegacyChunksFailed { get; protected set; }

        /// <summary>
        /// The deprecated chunk documents moved as new chunks, with their pins
        /// </summary>
        public virtual long LegacyChunksInserted { get; protected set; }

        /// <summary>
        /// The deprecated chunk documents of a chunk held already, whose pins were added to it
        /// </summary>
        public virtual long LegacyChunksMerged { get; protected set; }

        public virtual long LegacyChunksRead { get; protected set; }

        /// <summary>
        /// The deprecated chunk documents when the run started, as estimated by the db: null until it starts
        /// </summary>
        public virtual long? LegacyChunksTotal { get; protected set; }

        public virtual DateTime? StartedDateTime { get; protected set; }
        public virtual ChunksMigrationState State =>
            CompletedDateTime.HasValue ? //a run can fail before starting, when its job is gone
                IsStopped ? ChunksMigrationState.Stopped :
                FailureReason is null ? ChunksMigrationState.Completed :
                ChunksMigrationState.Failed :
            StartedDateTime.HasValue ? ChunksMigrationState.Running :
            ChunksMigrationState.Pending;

        /// <summary>
        /// When a stop was asked: the run stops at the end of the page it is migrating
        /// </summary>
        public virtual DateTime? StopRequestedDateTime { get; protected set; }

        // Methods.
        public virtual void Complete()
        {
            if (IsCompleted)
                throw new InvalidOperationException("Migration already completed");

            CompletedDateTime = DateTime.UtcNow;
        }

        public virtual void Fail(string failureReason)
        {
            ArgumentNullException.ThrowIfNull(failureReason);
            if (IsCompleted)
                throw new InvalidOperationException("Migration already completed");

            CompletedDateTime = DateTime.UtcNow;
            FailureReason = failureReason;
        }

        public virtual void GridFsFilesMigrated(long read, long inserted, long merged, long failed)
        {
            GridFsFilesRead += read;
            GridFsFilesInserted += inserted;
            GridFsFilesMerged += merged;
            GridFsFilesFailed += failed;
        }

        public virtual void LegacyChunksMigrated(long read, long inserted, long merged, long failed)
        {
            LegacyChunksRead += read;
            LegacyChunksInserted += inserted;
            LegacyChunksMerged += merged;
            LegacyChunksFailed += failed;
        }

        public virtual void RequestStop()
        {
            if (IsCompleted)
                throw new InvalidOperationException("Migration already completed");

            StopRequestedDateTime ??= DateTime.UtcNow;
        }

        public virtual void SetJobId(string jobId)
        {
            ArgumentNullException.ThrowIfNull(jobId);

            JobId = jobId;
        }

        /// <summary>
        /// Start the run, or start it again when its job was interrupted: the counters restart from zero, what the
        /// interrupted run migrated is not in the deprecated stores anymore
        /// </summary>
        public virtual void Start(long legacyChunksTotal, long gridFsFilesTotal)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(legacyChunksTotal);
            ArgumentOutOfRangeException.ThrowIfNegative(gridFsFilesTotal);
            if (IsCompleted)
                throw new InvalidOperationException("Migration already completed");

            GridFsFilesFailed = 0;
            GridFsFilesInserted = 0;
            GridFsFilesMerged = 0;
            GridFsFilesRead = 0;
            GridFsFilesTotal = gridFsFilesTotal;
            LegacyChunksFailed = 0;
            LegacyChunksInserted = 0;
            LegacyChunksMerged = 0;
            LegacyChunksRead = 0;
            LegacyChunksTotal = legacyChunksTotal;
            StartedDateTime = DateTime.UtcNow;
        }

        public virtual void StopOnRequest()
        {
            if (IsCompleted)
                throw new InvalidOperationException("Migration already completed");

            CompletedDateTime = DateTime.UtcNow;
            IsStopped = true;
        }
    }
}

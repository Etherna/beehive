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
using System;

namespace Etherna.Beehive.Areas.Api.DtoModels
{
    public sealed class ChunksMigrationDto(ChunksMigration migration)
    {
        public string Id { get; } = migration.Id;
        public ChunksMigrationState State { get; } = migration.State;

        /// <summary>
        /// The documents of the deprecated chunks collection, moved first
        /// </summary>
        public ChunksMigrationStoreDto LegacyChunks { get; } = new(
            migration.LegacyChunksTotal,
            migration.LegacyChunksRead,
            migration.LegacyChunksInserted,
            migration.LegacyChunksMerged,
            migration.LegacyChunksFailed);

        /// <summary>
        /// The files of the deprecated GridFS bucket, moved after the chunks collection
        /// </summary>
        public ChunksMigrationStoreDto GridFsFiles { get; } = new(
            migration.GridFsFilesTotal,
            migration.GridFsFilesRead,
            migration.GridFsFilesInserted,
            migration.GridFsFilesMerged,
            migration.GridFsFilesFailed);

        public string? Error { get; } = migration.FailureReason;
        public string? JobId { get; } = migration.JobId;
        public DateTime CreationDateTime { get; } = migration.CreationDateTime;
        public DateTime? StartedDateTime { get; } = migration.StartedDateTime;
        public DateTime? StopRequestedDateTime { get; } = migration.StopRequestedDateTime;
        public DateTime? CompletedDateTime { get; } = migration.CompletedDateTime;
    }
}

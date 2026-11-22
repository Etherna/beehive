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

namespace Etherna.Beehive.Areas.Api.DtoModels
{
    /// <summary>
    /// The progress of a chunks migration on one deprecated store
    /// </summary>
    public sealed class ChunksMigrationStoreDto(
        long? total,
        long read,
        long inserted,
        long merged,
        long failed)
    {
        /// <summary>
        /// The chunks in the store when the migration started, as estimated by the db: null until it starts
        /// </summary>
        public long? Total { get; } = total;

        public long Read { get; } = read;

        /// <summary>
        /// The chunks moved as new chunks
        /// </summary>
        public long Inserted { get; } = inserted;

        /// <summary>
        /// The chunks held already: their pins were added to the held chunk, and they left the store
        /// </summary>
        public long Merged { get; } = merged;

        /// <summary>
        /// The chunks that could not be moved, left in the store
        /// </summary>
        public long Failed { get; } = failed;
    }
}

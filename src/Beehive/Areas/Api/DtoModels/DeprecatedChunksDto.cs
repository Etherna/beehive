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
    /// The chunks still in the deprecated stores, as estimated by the db
    /// </summary>
    public sealed class DeprecatedChunksDto(
        long legacyChunks,
        long gridFsFiles)
    {
        /// <summary>
        /// The documents of the deprecated chunks collection
        /// </summary>
        public long LegacyChunks { get; } = legacyChunks;

        /// <summary>
        /// The files of the deprecated GridFS bucket
        /// </summary>
        public long GridFsFiles { get; } = gridFsFiles;
    }
}

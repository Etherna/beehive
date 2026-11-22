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

namespace Etherna.Beehive.Services.Utilities.Models
{
    /// <summary>
    /// A document of the deprecated chunks collection: its chunk, or why it could not be read
    /// </summary>
    public sealed class LegacyChunkDocument
    {
        // Constructors.
        public LegacyChunkDocument(LegacyChunk chunk)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            Chunk = chunk;
            Id = chunk.Id;
        }

        public LegacyChunkDocument(string id, string readFailure)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(readFailure);

            Id = id;
            ReadFailure = readFailure;
        }

        // Properties.
        /// <summary>
        /// The chunk: null when the document could not be read
        /// </summary>
        public LegacyChunk? Chunk { get; }

        public string Id { get; }

        /// <summary>
        /// Why the document could not be read: null when it was
        /// </summary>
        public string? ReadFailure { get; }
    }
}

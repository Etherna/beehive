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

using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Services.Utilities.Models
{
    /// <summary>
    /// The integrity of a pin read from beehive's own chunks: the chunks of its content, the ones beehive doesn't
    /// hold, and the invalid ones, whose payload is not the chunk at their address or not a node of the content's
    /// tree
    /// </summary>
    public sealed class PinCheckResult(
        SwarmReference reference,
        long totalChunks,
        IReadOnlyCollection<SwarmHash> missingChunks,
        IReadOnlyCollection<SwarmHash> invalidChunks,
        long referencedChunks,
        SwarmHash? malformedChunk = null)
    {
        public IReadOnlyCollection<SwarmHash> InvalidChunks { get; } = invalidChunks ?? throw new ArgumentNullException(nameof(invalidChunks));
        public bool IsComplete => InvalidChunks.Count == 0 && MissingChunks.Count == 0;

        /// <summary>
        /// The invalid chunk the traversal stopped at, when there is one: its payload is the chunk at its address,
        /// but not a well formed node of the content's tree. The chunks of the content below it, and the ones not
        /// met before it, are not counted
        /// </summary>
        public SwarmHash? MalformedChunk { get; } = malformedChunk;
        public IReadOnlyCollection<SwarmHash> MissingChunks { get; } = missingChunks ?? throw new ArgumentNullException(nameof(missingChunks));
        public SwarmReference Reference { get; } = reference;

        /// <summary>
        /// The chunks beehive held without the pin among their references, which the check attached to them
        /// </summary>
        public long ReferencedChunks { get; } = referencedChunks;

        /// <summary>
        /// The chunks of the content: the ones met by the traversal, found or not
        /// </summary>
        public long TotalChunks { get; } = totalChunks;
    }
}

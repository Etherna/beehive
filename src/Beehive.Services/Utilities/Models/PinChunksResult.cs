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
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Services.Utilities.Models
{
    /// <summary>
    /// The outcome of pinning a content's chunks: the pin as processed, and the chunks the store answered
    /// with something that isn't a content addressed chunk
    /// </summary>
    public sealed class PinChunksResult(
        ChunkPin pin,
        IReadOnlyCollection<SwarmHash> invalidChunks)
    {
        public IReadOnlyCollection<SwarmHash> InvalidChunks { get; } = invalidChunks ?? throw new ArgumentNullException(nameof(invalidChunks));
        public ChunkPin Pin { get; } = pin ?? throw new ArgumentNullException(nameof(pin));
    }
}

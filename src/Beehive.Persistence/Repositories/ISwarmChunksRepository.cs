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
using Etherna.Scrinium.Core.Repositories;
using Etherna.SwarmSdk.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Persistence.Repositories
{
    /// <summary>
    /// The chunks keyed by hash, one document per hash whoever creates it: a creation of a chunk held already
    /// doesn't fail, the held chunk takes the pins of the created one. Every creation follows this rule
    /// </summary>
    public interface ISwarmChunksRepository : IRepository<Chunk, SwarmHash>
    {
        /// <summary>
        /// Create chunks, one document per hash, inserting them optimistically: the db is read only when the insert
        /// is refused for a hash it holds already, by now or because a concurrent writer created it. Then the held
        /// ones take the pins of the created ones, and the others are created again in two halves, each inserted
        /// optimistically in turn: every refusal leaves one more hash held, and the halves shrink down to a single
        /// chunk, which a refusal proves held
        /// </summary>
        /// <returns>The chunks found held, that took the pins of the created ones instead of being inserted</returns>
        Task<int> CreateOrAddPinsAsync(
            IEnumerable<Chunk> chunks,
            CancellationToken cancellationToken = default);
    }
}

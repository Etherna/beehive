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

namespace Etherna.Beehive.Domain.Models
{
    /// <summary>
    /// What a pin declares of its content (<see cref="ChunkPin.State"/>). Every pin is in one state, and one only
    /// </summary>
    public enum ChunkPinState
    {
        /// <summary>
        /// Processed, with chunks of its content beehive doesn't hold
        /// </summary>
        Incomplete,

        /// <summary>
        /// Processed, with every chunk of its content
        /// </summary>
        Succeeded,

        /// <summary>
        /// Not processed yet: its pinning didn't run, or didn't end
        /// </summary>
        Unprocessed
    }
}

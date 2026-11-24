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

namespace Etherna.Beehive.Areas.Api.DtoModels
{
    /// <summary>
    /// The integrity of a pin, in bee's shape: the chunks not held, the invalid ones, and every chunk of its content.
    /// Declared in reading order: the reference first, the total after its parts
    /// </summary>
    public sealed class BeePinCheckDto(
        SwarmReference reference,
        long missing,
        long invalid,
        long total)
    {
        public SwarmReference Reference { get; } = reference;
        public long Missing { get; } = missing;
        public long Invalid { get; } = invalid;
        public long Total { get; } = total;
    }
}

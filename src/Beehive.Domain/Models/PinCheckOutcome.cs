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
    public enum PinCheckOutcome
    {
        /// <summary>
        /// Beehive holds the whole content
        /// </summary>
        Complete,

        /// <summary>
        /// The content could not be checked
        /// </summary>
        Failed,

        /// <summary>
        /// Beehive doesn't hold some chunks of the content, and none of the ones it holds is invalid
        /// </summary>
        Incomplete,

        /// <summary>
        /// Some chunks of the content are invalid
        /// </summary>
        Invalid
    }
}

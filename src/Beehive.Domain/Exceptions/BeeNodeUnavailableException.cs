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

using System;

namespace Etherna.Beehive.Domain.Exceptions
{
    /// <summary>
    /// Thrown for a bee node that cannot serve a call: not alive for its heartbeat, not answering, or answering an error. The message is the node's reason
    /// </summary>
    public sealed class BeeNodeUnavailableException : Exception
    {
        public BeeNodeUnavailableException()
        {
        }

        public BeeNodeUnavailableException(string message) : base(message)
        {
        }

        public BeeNodeUnavailableException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

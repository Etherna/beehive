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
using Etherna.Scrinium.Core.Utility;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Helpers
{
    /// <summary>
    /// A granted resource lock lease: never lost, released by its dispose
    /// </summary>
    internal sealed class FakeResourceLockLease : IResourceLockLease
    {
        // Properties.
        public bool IsReleased { get; private set; }
        public CancellationToken LeaseLostToken => CancellationToken.None;
        public string OwnerId { get; } = Guid.NewGuid().ToString();

        // Methods.
        public ValueTask DisposeAsync()
        {
            IsReleased = true;
            return ValueTask.CompletedTask;
        }
    }
}

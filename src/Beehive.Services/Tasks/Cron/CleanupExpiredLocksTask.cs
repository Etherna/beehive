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

using Etherna.Beehive.Domain;
using Etherna.MongoDB.Driver.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Cron
{
    public class CleanupExpiredLocksTask(IBeehiveDbContext dbContext)
        : ICleanupExpiredLocksTask
    {
        // Consts.
        public const string TaskId = "cleanupExpiredLocksTask";

        // Methods.
        public async Task RunAsync()
        {
            var expiredLocks = await dbContext.ChunkPinLocks.QueryElementsAsync(elements =>
                elements.Where(l => l.ExpirationTime < DateTime.UtcNow)
                    .ToListAsync());

            foreach (var expiredLock in expiredLocks)
                await dbContext.ChunkPinLocks.DeleteAsync(expiredLock);
        }
    }
}
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
using Etherna.Beehive.Domain.Models;
using System;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public class ChunkPinLockService(IBeehiveDbContext dbContext)
        : IChunkPinLockService
    {
        // Methods.
        public async Task<bool> AcquireLockAsync(
            string chunkPinId,
            string taskId)
        {
            var prevLock = await dbContext.ChunkPinLocks.TryFindOneAsync(l => l.ChunkPinId == chunkPinId);
            
            // If prev lock exists, verify if is expired.
            if (prevLock?.ExpirationTime > DateTime.UtcNow)
                return false;
            
            // If here, prev lock doesn't exist or is expired. Create new one.
            var pinLock = new ChunkPinLock(chunkPinId, taskId);
            await dbContext.ChunkPinLocks.CreateAsync(pinLock);
            
            // Delete old lock, if existing.
            if (prevLock is not null)
                await dbContext.ChunkPinLocks.DeleteAsync(prevLock.Id);
            
            return true;
        }
        
        public async Task<bool> IsResourceLockedAsync(string chunkPinId)
        {
            var pinLock = await dbContext.ChunkPinLocks.TryFindOneAsync(l => l.ChunkPinId == chunkPinId);
            return pinLock != null && pinLock.ExpirationTime > DateTime.UtcNow;
        }
        
        public async Task<bool> ReleaseLockAsync(string chunkPinId, string jobId)
        {
            var pinLock = await dbContext.ChunkPinLocks.TryFindOneAsync(l => l.ChunkPinId == chunkPinId);
        
            if (pinLock != null && pinLock.JobId == jobId)
            {
                await dbContext.ChunkPinLocks.DeleteAsync(pinLock);
                await dbContext.SaveChangesAsync();
                return true;
            }
        
            //lock not found, or job different from current
            return false;
        }
    }
}
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
using Etherna.MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public class ChunkPinLockService(IBeehiveDbContext dbContext)
        : IChunkPinLockService
    {
        // Methods.
        public async Task<bool> AcquireLockAsync(string chunkPinId)
        {
            // If prev lock exists, verify if is expired.
            var prevLock = await dbContext.ChunkPinLocks.TryFindOneAsync(l => l.ChunkPinId == chunkPinId);
            if (prevLock?.ExpirationTime > DateTime.UtcNow)
                return false;
            
            // Delete old expired lock, if exists.
            if (prevLock is not null)
            {
                try
                {
                    await dbContext.ChunkPinLocks.DeleteAsync(prevLock);
                }
                catch (MongoWriteException) { }
            }
            
            // If here, prev lock doesn't exist anymore. Create new one.
            // It could still fail here because of concurrent accesses,
            // in this case unique index on chunkPinId permits only one to proceed.
            var pinLock = new ChunkPinLock(chunkPinId);
            try
            {
                await dbContext.ChunkPinLocks.CreateAsync(pinLock);
            }
            catch (MongoWriteException)
            {
                return false;
            }
            
            return true;
        }
        
        public async Task<bool> IsResourceLockedAsync(string chunkPinId)
        {
            var pinLock = await dbContext.ChunkPinLocks.TryFindOneAsync(l => l.ChunkPinId == chunkPinId);
            return pinLock != null && pinLock.ExpirationTime > DateTime.UtcNow;
        }
        
        public async Task<bool> ReleaseLockAsync(string chunkPinId)
        {
            var pinLock = await dbContext.ChunkPinLocks.TryFindOneAsync(l => l.ChunkPinId == chunkPinId);
        
            if (pinLock != null)
            {
                await dbContext.ChunkPinLocks.DeleteAsync(pinLock);
                await dbContext.SaveChangesAsync();
                return true;
            }
        
            //lock not found
            return false;
        }
    }
}
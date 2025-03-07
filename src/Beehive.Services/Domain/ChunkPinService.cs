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
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public class ChunkPinService(
        IBeehiveDbContext dbContext,
        IResourceLockService resourceLockService)
        : IChunkPinService
    {
        // Methods.
        public Task<bool> AcquireLockAsync(
            string chunkPinId,
            bool exclusiveAccess) =>
            resourceLockService.AcquireLockAsync(
                () => new ChunkPinLock(chunkPinId, exclusiveAccess),
                dbContext.ChunkPinLocks,
                chunkPinId,
                exclusiveAccess);

        public Task<bool> IsLockedAsync(string chunkPinId) =>
            resourceLockService.IsLockedAsync(
                dbContext.ChunkPinLocks,
                chunkPinId);

        public Task<bool> ReleaseLockAsync(
            string chunkPinId,
            bool exclusiveAccess) =>
            resourceLockService.ReleaseLockAsync(
                dbContext.ChunkPinLocks,
                chunkPinId,
                exclusiveAccess);
    }
}
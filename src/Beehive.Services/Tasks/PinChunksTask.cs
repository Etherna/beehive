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
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Hangfire.Server;
using System;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks
{
    public class PinChunksTask(
        IBeeNodeLiveManager beeNodeLiveManager,
        IChunkPinLockService chunkPinLockService,
        IBeehiveDbContext dbContext)
        : IPinChunksTask
    {
        // Methods.
        public async Task RunAsync(
            string chunkPinId,
            PerformContext hangfireContext)
        {
            var jobId = hangfireContext?.BackgroundJob.Id ?? "";
            
            // Acquire lock on pin.
            if (!await chunkPinLockService.AcquireLockAsync(chunkPinId, jobId))
                throw new InvalidOperationException($"Pin {chunkPinId} is locked by another job");

            try
            {
                var pin = await dbContext.ChunkPins.FindOneAsync(chunkPinId);
                if (!pin.Hash.HasValue)
                    throw new InvalidOperationException($"Pin doesn't have an hash");
                if (pin.IsSucceeded)
                    return;

                using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext);
                var chunkTraverser = new ChunkTraverser(chunkStore);

                await chunkTraverser.TraverseFromMantarayManifestRootAsync(
                    pin.Hash.Value,
                    foundChunk =>
                    {
                        //TODO
                        return Task.CompletedTask;
                    },
                    notFoundHash =>
                    {
                        //TODO
                        return Task.CompletedTask;
                    });
            }
            finally
            {
                // Release lock.
                await chunkPinLockService.ReleaseLockAsync(chunkPinId, jobId);
            }
        }
    }
}
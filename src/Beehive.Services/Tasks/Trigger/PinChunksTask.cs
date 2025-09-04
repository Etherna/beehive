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
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core.Serialization.Modifiers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Trigger
{
    public class PinChunksTask(
        IBeeNodeLiveManager beeNodeLiveManager,
        IPinService pinService,
        IBeehiveDbContext dbContext,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IPinChunksTask
    {
        // Methods.
        public async Task RunAsync(string chunkPinId)
        {
            // Acquire lock on pin.
            await using var pinLockHandler = await pinService.AcquireLockAsync(chunkPinId, true);

            var pin = await dbContext.ChunkPins.FindOneAsync(chunkPinId);
            if (!pin.Reference.HasValue)
                throw new InvalidOperationException($"Pin doesn't have an hash");
            if (pin.IsSucceeded)
                return;

            // Traverse chunks.
            await using var chunkStore =
                new BeehiveChunkStore(beeNodeLiveManager, dbContext, serializerModifierAccessor);
            var chunkTraverser = new ChunkTraverser(chunkStore);

            HashSet<SwarmHash> missingChunksHash = [];
            HashSet<SwarmHash> pinnedChunksHash = [];
            await chunkTraverser.TraverseAsync(
                pin.Reference.Value,
                async foundChunk =>
                {
                    if (!pinnedChunksHash.Add(foundChunk.Hash))
                        return;
                    await dbContext.Chunks.TryFindOneAndAddToSetAsync(
                        new ExpressionFilterDefinition<Chunk>(c => c.Hash == foundChunk.Hash),
                        c => c.Pins,
                        pin,
                        new FindOneAndUpdateOptions<Chunk>());
                },
                async invalidFoundChunk =>
                {
                    if (!pinnedChunksHash.Add(invalidFoundChunk.Hash))
                        return;
                    await dbContext.Chunks.TryFindOneAndAddToSetAsync(
                        new ExpressionFilterDefinition<Chunk>(c => c.Hash == invalidFoundChunk.Hash),
                        c => c.Pins,
                        pin,
                        new FindOneAndUpdateOptions<Chunk>());
                },
                notFoundHash =>
                {
                    missingChunksHash.Add(notFoundHash);
                    return Task.CompletedTask;
                });
            
            // Update pin with result.
            pin.UpdateProcessed(missingChunksHash, pinnedChunksHash.Count);
            await dbContext.SaveChangesAsync();
        }
    }
}
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
using Etherna.Beehive.Domain.Exceptions;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Chunks;
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public sealed class PinService(
        IBeehiveDbContext dbContext,
        IResourceLockService resourceLockService,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IPinService
    {
        // Methods.
        public async Task<ResourceLockHandler<ChunkPinLock>> AcquireLockAsync(
            string chunkPinId,
            bool exclusiveAccess)
        {
            var handler = await resourceLockService.TryAcquireLockAsync(
                () => new ChunkPinLock(chunkPinId, exclusiveAccess),
                dbContext.ChunkPinLocks,
                chunkPinId,
                exclusiveAccess);
            
            if (handler is null)
                throw new ResourceLockException();

            return handler;
        }

        public Task<bool> IsLockedAsync(string chunkPinId) =>
            resourceLockService.IsLockedAsync(
                dbContext.ChunkPinLocks,
                chunkPinId);

        public async Task<PinChunksResult> PinChunksAsync(string chunkPinId, BeehiveChunkStore chunkStore)
        {
            ArgumentNullException.ThrowIfNull(chunkStore);

            // Acquire lock on pin.
            await using var pinLockHandler = await AcquireLockAsync(chunkPinId, true);

            var pin = await dbContext.ChunkPins.FindOneAsync(chunkPinId);
            if (!pin.Reference.HasValue)
                throw new InvalidOperationException("Pin doesn't have an hash");
            if (pin.IsSucceeded)
                return new PinChunksResult(pin, []);

            // Traverse chunks.
            var chunkTraverser = new ChunkTraverser(chunkStore);

            HashSet<SwarmHash> invalidChunksHash = [];
            HashSet<SwarmHash> missingChunksHash = [];
            HashSet<SwarmHash> pinnedChunksHash = [];
            /* The traversal reads back one chunk document per pinned chunk: keep them out of
             * the identity map of this scope, or it grows with the pinned tree. */
            using (serializerModifierAccessor.EnableCacheSerializerModifier(true))
            {
                await chunkTraverser.TraverseAsync(
                    pin.Reference.Value,
                    async (foundChunk, _) =>
                    {
                        if (!pinnedChunksHash.Add(foundChunk.Hash))
                            return;
                        await dbContext.Chunks.TryFindOneAndAddToSetAsync(
                            new ExpressionFilterDefinition<Chunk>(c => c.Hash == foundChunk.Hash),
                            c => c.Pins,
                            pin,
                            new FindOneAndUpdateOptions<Chunk>());
                    },
                    async (invalidFoundChunk, _) =>
                    {
                        invalidChunksHash.Add(invalidFoundChunk.Hash);
                        if (!pinnedChunksHash.Add(invalidFoundChunk.Hash))
                            return;
                        await dbContext.Chunks.TryFindOneAndAddToSetAsync(
                            new ExpressionFilterDefinition<Chunk>(c => c.Hash == invalidFoundChunk.Hash),
                            c => c.Pins,
                            pin,
                            new FindOneAndUpdateOptions<Chunk>());
                    },
                    notFoundReference =>
                    {
                        missingChunksHash.Add(notFoundReference.Reference.Hash);
                        return Task.CompletedTask;
                    });
            }

            // Update pin with result.
            pin.UpdateProcessed(missingChunksHash, pinnedChunksHash.Count);
            await dbContext.SaveChangesAsync();

            return new PinChunksResult(pin, invalidChunksHash);
        }

        public async Task<bool> TryDeletePinAsync(SwarmReference pinReference)
        {
            // Try find pin and acquire lock on it.
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Reference == pinReference);
            if (pin is null)
                return false;
            
            await using var pinLockHandler = await AcquireLockAsync(pin.Id, true);
            
            // Delete it. Its references are pulled from the chunks in background, by the delete
            // policy declared on the chunk pins reference serializer.
            await dbContext.ChunkPins.DeleteAsync(pin);
            
            return true;
        }
    }
}
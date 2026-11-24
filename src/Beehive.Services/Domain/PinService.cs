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

using Etherna.Beehive.Domain.Exceptions;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Chunks;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public sealed class PinService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        ILogger<PinService> logger,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IPinService
    {
        // Methods.
        public async Task<IResourceLockLease> AcquireLockAsync(
            string chunkPinId,
            bool exclusiveAccess)
        {
            var lease = await dbContext.TryAcquireResourceLockAsync(
                ResourceLockNamespaces.ChunkPins,
                chunkPinId,
                exclusiveAccess ? ResourceLockMode.Exclusive : ResourceLockMode.Shared);
            
            if (lease is null)
                throw new ResourceLockException($"Pin {chunkPinId} is locked by another operation");

            return lease;
        }

        public Task<bool> IsLockedAsync(string chunkPinId) =>
            dbContext.IsResourceLockedAsync(ResourceLockNamespaces.ChunkPins, chunkPinId);

        public async Task<PinChunksResult> PinChunksAsync(
            string chunkPinId,
            BeeNodeLiveInstance? sourceNode,
            CancellationToken cancellationToken = default)
        {
            // Acquire lock on pin. A cancellation releases it with the lease, and the work stops when the lease is
            // lost: the pin is not this operation's anymore.
            await using var pinLockLease = await AcquireLockAsync(chunkPinId, true);
            using var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                pinLockLease.LeaseLostToken);
            var workToken = workCancellation.Token;

            try
            {
                var pin = await dbContext.ChunkPins.FindOneAsync(chunkPinId, workToken);
                if (!pin.Reference.HasValue)
                    throw new InvalidOperationException("Pin doesn't have an hash");
                if (pin.State == ChunkPinState.Succeeded)
                    return new PinChunksResult(pin, []);

                // Traverse chunks. The store saves the chunks it reads from the nodes with the pin attached, so a chunk
                // beehive didn't hold enters the db pinned; a chunk it held takes the pin on its document.
                HashSet<SwarmHash> invalidChunksHash = [];
                HashSet<SwarmHash> missingChunksHash = [];
                HashSet<SwarmHash> pinnedChunksHash = [];
                await using var chunkStore = new BeehiveChunkStore(
                    beeNodeLiveManager,
                    dbContext,
                    logger,
                    serializerModifierAccessor,
                    onSavingChunk: c => c.AddPin(pin),
                    sourceNode: sourceNode);
                var chunkTraverser = new ChunkTraverser(chunkStore);

                /* The traversal reads back one chunk document per pinned chunk: keep them out of
                 * the identity map of this scope, or it grows with the pinned tree. */
                using (serializerModifierAccessor.EnableCacheSerializerModifier(true))
                {
                    await chunkTraverser.TraverseAsync(
                        pin.Reference.Value,
                        (foundChunk, _) =>
                            PinFoundChunkAsync(pin, foundChunk.Hash, pinnedChunksHash, missingChunksHash, workToken),
                        (invalidFoundChunk, _) =>
                        {
                            invalidChunksHash.Add(invalidFoundChunk.Hash);
                            return PinFoundChunkAsync(pin, invalidFoundChunk.Hash, pinnedChunksHash, missingChunksHash, workToken);
                        },
                        notFoundReference =>
                        {
                            missingChunksHash.Add(notFoundReference.Reference.Hash);
                            return Task.CompletedTask;
                        },
                        cancellationToken: workToken);
                }

                // Verify the pin on the db before declaring it: a chunk is pinned by its document carrying the pin,
                // not by the traversal having met it. A chunk not migrated yet carries it in the deprecated chunks.
                var storedSwarmChunksHash = await dbContext.SwarmChunks.QueryElementsAsync(elements =>
                    elements.Where(c => c.Pins.Any(p => p.Id == pin.Id))
                        .Select(c => c.Id)
                        .ToListAsync(workToken));
                var storedChunksHash = await dbContext.Chunks.QueryElementsAsync(elements =>
                    elements.Where(c => c.Pins.Any(p => p.Id == pin.Id))
                        .Select(c => c.Hash)
                        .ToListAsync(workToken));
                foreach (var hash in pinnedChunksHash.Except(storedSwarmChunksHash).Except(storedChunksHash).ToArray())
                {
                    pinnedChunksHash.Remove(hash);
                    missingChunksHash.Add(hash);
                }

                // Update pin with result, never under a lease lost meanwhile.
                pinLockLease.LeaseLostToken.ThrowIfCancellationRequested();
                pin.UpdateProcessed(missingChunksHash, pinnedChunksHash.Count);
                await dbContext.SaveChangesAsync(workToken);
                logger.PinProcessed(pin.Reference.Value, pinnedChunksHash.Count, missingChunksHash.Count, invalidChunksHash.Count);

                return new PinChunksResult(pin, invalidChunksHash);
            }
            catch (OperationCanceledException e) when (pinLockLease.LeaseLostToken.IsCancellationRequested)
            {
                throw new ResourceLockException($"Pin {chunkPinId} lost its lock while it was being pinned", e);
            }
        }

        public async Task<bool> TryDeletePinAsync(SwarmReference pinReference)
        {
            // Try find pin and acquire lock on it.
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Reference == pinReference);
            if (pin is null)
                return false;
            
            await using var pinLockLease = await AcquireLockAsync(pin.Id, true);
            
            // Delete it. Its references are pulled from the chunks in background, by the delete
            // policy declared on the chunk pins reference serializer.
            await dbContext.ChunkPins.DeleteAsync(pin);
            logger.PinDeleted(pinReference);
            
            return true;
        }

        // Helpers.
        /// <summary>
        /// Attach the pin to the document of a chunk the traversal met, once per chunk: the chunk is pinned when a
        /// document took the pin (saved with it by the store, or held by beehive already), missing otherwise. A chunk
        /// not migrated yet from the deprecated chunks of 0.4.0 takes the pin there: its migration moves its pins
        /// </summary>
        private async Task PinFoundChunkAsync(
            ChunkPin pin,
            SwarmHash hash,
            HashSet<SwarmHash> pinnedChunksHash,
            HashSet<SwarmHash> missingChunksHash,
            CancellationToken cancellationToken)
        {
            if (pinnedChunksHash.Contains(hash) || missingChunksHash.Contains(hash))
                return;

            var chunkDocument = await dbContext.SwarmChunks.TryFindOneAndAddToSetAsync(
                new ExpressionFilterDefinition<Chunk>(c => c.Id == hash),
                c => c.Pins,
                pin,
                new FindOneAndUpdateOptions<Chunk>(),
                cancellationToken);
            var legacyChunkDocument = chunkDocument is null ?
                await dbContext.Chunks.TryFindOneAndAddToSetAsync(
                    new ExpressionFilterDefinition<LegacyChunk>(c => c.Hash == hash),
                    c => c.Pins,
                    pin,
                    new FindOneAndUpdateOptions<LegacyChunk>(),
                    cancellationToken) :
                null;
            if (chunkDocument is not null || legacyChunkDocument is not null)
                pinnedChunksHash.Add(hash);
            else
                missingChunksHash.Add(hash);
        }
    }
}
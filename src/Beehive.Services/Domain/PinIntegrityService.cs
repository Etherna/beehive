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
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public sealed class PinIntegrityService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        IDeprecatedChunkStores deprecatedChunkStores,
        ILogger<PinIntegrityService> logger,
        IPinService pinService,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IPinIntegrityService
    {
        // Consts.
        private const string PinsCheckLockId = "all";

        // Methods.
        public async Task<PinCheckResult> CheckPinsAsync(
            SwarmReference reference,
            bool dryRun,
            CancellationToken cancellationToken = default)
        {
            // Traverse the content on beehive's own chunks: a chunk only a node holds is a chunk beehive doesn't.
            HashSet<SwarmHash> foundChunksHash = [];
            HashSet<SwarmHash> invalidChunksHash = [];
            HashSet<SwarmHash> missingChunksHash = [];
            SwarmHash? malformedChunkHash = null;
            var swarmChunkBmt = new SwarmChunkBmt();
            await using (var chunkStore = new BeehiveChunkStore(
                             beeNodeLiveManager,
                             dbContext,
                             logger,
                             serializerModifierAccessor,
                             readFromNodes: false))
            {
                try
                {
                    /* The traversal reads one chunk document per chunk of the content: keep them out of the identity
                     * map of this scope, or it grows with the checked tree. */
                    using (serializerModifierAccessor.EnableCacheSerializerModifier(true))
                    {
                        await new ChunkTraverser(chunkStore).TraverseAsync(
                            reference,
                            (foundChunk, _) =>
                                ClassifyFoundChunkAsync(foundChunk, swarmChunkBmt, foundChunksHash, invalidChunksHash),
                            (invalidFoundChunk, _) => //a soc where a manifest node was expected: a chunk held all the same
                                ClassifyFoundChunkAsync(invalidFoundChunk, swarmChunkBmt, foundChunksHash, invalidChunksHash),
                            notFoundReference =>
                            {
                                missingChunksHash.Add(notFoundReference.Reference.Hash);
                                return Task.CompletedTask;
                            },
                            cancellationToken: cancellationToken);
                    }
                }
                catch (SwarmChunkVerificationException e) when (e.Hash == reference.Hash)
                {
                    // The root is a single owner chunk document that doesn't verify: the whole content is that chunk.
                    invalidChunksHash.Add(reference.Hash);
                }
                catch (SwarmChunkFormatException e)
                {
                    // A chunk that is not a well formed node of the tree: the traversal can't read below it, and
                    // stops there. It is invalid for this content, and what was not met yet is not counted.
                    malformedChunkHash = e.Hash;
                    foundChunksHash.Remove(e.Hash);
                    invalidChunksHash.Add(e.Hash);
                }
            }

            // Every valid chunk found must carry each pin of the reference on its document, in the chunks keyed by
            // hash: the ones held without a pin take it now, unless the check is a dry run. A chunk found in a
            // deprecated store has no document there, and is missing.
            long referencedChunks = 0;
            var pins = dryRun ?
                [] :
                await dbContext.ChunkPins.QueryElementsAsync(elements =>
                    elements.Where(p => p.Reference == reference)
                        .ToListAsync(cancellationToken));
            foreach (var pin in pins)
            {
                var referencingChunksHash = await dbContext.SwarmChunks.QueryElementsAsync(elements =>
                    elements.Where(c => c.Pins.Any(p => p.Id == pin.Id))
                        .Select(c => c.Id)
                        .ToListAsync(cancellationToken));
                foreach (var hash in foundChunksHash.Except(referencingChunksHash))
                {
                    var chunkDocument = await dbContext.SwarmChunks.TryFindOneAndAddToSetAsync(
                        new ExpressionFilterDefinition<Chunk>(c => c.Id == hash),
                        c => c.Pins,
                        pin,
                        new FindOneAndUpdateOptions<Chunk>(),
                        cancellationToken);
                    if (chunkDocument is null)
                        missingChunksHash.Add(hash);
                    else
                        referencedChunks++;
                }
            }
            if (referencedChunks > 0)
                logger.PinChunksReferenced(reference, referencedChunks);

            var totalChunks = foundChunksHash
                .Union(invalidChunksHash)
                .Union(missingChunksHash)
                .Count();
            logger.PinChecked(reference, totalChunks, missingChunksHash.Count, invalidChunksHash.Count);

            return new PinCheckResult(
                reference,
                totalChunks,
                missingChunksHash,
                invalidChunksHash,
                referencedChunks,
                malformedChunkHash);
        }

        public async Task<ChunkPin> CorrectPinAsync(
            ChunkPin pin,
            PinCheckResult checkResult,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pin);
            ArgumentNullException.ThrowIfNull(checkResult);

            if (!checkResult.IsComplete)
                return await ResetPinAsync(pin.Id, checkResult, cancellationToken);
            if (pin.State == ChunkPinState.Succeeded) //as it says: nothing to write, no lock to take
                return pin;

            // A pin declared not succeeded holds its whole content all the same: every chunk the check met carries
            // it, the ones held without it took it from the check.
            await using var pinLockLease = await pinService.AcquireLockAsync(pin.Id, true);
            pinLockLease.LeaseLostToken.ThrowIfCancellationRequested();
            pin.UpdateProcessed([], checkResult.TotalChunks);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.PinConfirmed(checkResult.Reference, checkResult.TotalChunks);

            return pin;
        }

        public async Task<ChunkPin> ResetPinAsync(
            string chunkPinId,
            PinCheckResult checkResult,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(checkResult);

            await using var pinLockLease = await pinService.AcquireLockAsync(chunkPinId, true);
            var pin = await dbContext.ChunkPins.FindOneAsync(chunkPinId, cancellationToken);

            // Delete the documents of the corrupt chunks: a pinning reads beehive's chunks first, and would take the
            // corrupt copy again instead of reading the chunk from the nodes. A malformed chunk stays: its payload
            // is the chunk at its address, a node would serve the same one.
            var corruptChunksHash = checkResult.InvalidChunks.Where(h => h != checkResult.MalformedChunk).ToArray();
            if (corruptChunksHash.Length > 0 && //none left to delete when another pin of the reference was reset before
                await dbContext.SwarmChunks.DeleteManyAsync(c => corruptChunksHash.Contains(c.Id), cancellationToken) > 0)
                logger.InvalidChunksDeleted(checkResult.Reference, corruptChunksHash);

            // The pin holds every chunk of its content but the missing and the invalid ones.
            pinLockLease.LeaseLostToken.ThrowIfCancellationRequested();
            pin.UpdateProcessed(
                checkResult.MissingChunks.Concat(checkResult.InvalidChunks),
                checkResult.TotalChunks - checkResult.MissingChunks.Count - checkResult.InvalidChunks.Count);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.PinReset(checkResult.Reference, checkResult.MissingChunks.Count, checkResult.InvalidChunks.Count);

            return pin;
        }

        public Task<IResourceLockLease?> TryAcquirePinsCheckLockAsync() =>
            dbContext.TryAcquireResourceLockAsync(ResourceLockNamespaces.PinChecks, PinsCheckLockId);

        public async Task VerifyPinsCanBeCheckedAsync(CancellationToken cancellationToken = default)
        {
            if ((await deprecatedChunkStores.ReadLegacyChunksAsync(null, 1, cancellationToken)).Count > 0 ||
                (await deprecatedChunkStores.ReadGridFsFilesAsync(null, 1, cancellationToken)).Count > 0)
                throw new DeprecatedChunksNotMigratedException(
                    "the deprecated chunk stores are not empty: migrate their chunks first");
        }

        // Helpers.
        /// <summary>
        /// Count a chunk the traversal found as held, or as invalid when its payload is not the chunk at its address.
        /// A content addressed chunk is built from its document as it is, and verified here; a single owner chunk is
        /// verified by its build, which a document that doesn't verify fails
        /// </summary>
        private static Task ClassifyFoundChunkAsync(
            SwarmChunk chunk,
            SwarmChunkBmt swarmChunkBmt,
            HashSet<SwarmHash> foundChunksHash,
            HashSet<SwarmHash> invalidChunksHash)
        {
            if (chunk is SwarmCac cac && !SwarmCac.IsValid(cac.Hash, cac.SpanData, swarmChunkBmt))
                invalidChunksHash.Add(chunk.Hash);
            else
                foundChunksHash.Add(chunk.Hash);
            return Task.CompletedTask;
        }
    }
}

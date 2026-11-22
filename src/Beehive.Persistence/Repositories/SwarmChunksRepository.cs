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

using Etherna.Beehive.Domain.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Repositories;
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Persistence.Repositories
{
    /// <summary>
    /// The chunks keyed by hash: every creation adds the pins of a chunk held already to its document, instead of
    /// failing on its duplicate key
    /// </summary>
    public class SwarmChunksRepository(RepositoryOptions<Chunk> options)
        : Repository<Chunk, SwarmHash>(options), ISwarmChunksRepository
    {
        // Methods.
        public override Task CreateAsync(IEnumerable<Chunk> models, CancellationToken cancellationToken = default) =>
            CreateOrAddPinsAsync(models, cancellationToken);

        public override Task CreateAsync(Chunk model, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(model);
            return CreateOrAddPinsAsync([model], cancellationToken);
        }

        public async Task<int> CreateOrAddPinsAsync(
            IEnumerable<Chunk> chunks,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(chunks);

            var chunksArray = chunks.ToArray();
            if (chunksArray.Length == 0)
                return 0;
            try
            {
                await InsertChunksAsync(chunksArray, cancellationToken);
                return 0;
            }
            catch (Exception e) when (IsDuplicateKeyFailure(e))
            {
                //a hash is held: by now, or created meanwhile by a concurrent writer
            }

            // A single chunk refused is held.
            if (chunksArray.Length == 1)
            {
                await AddPinsToHeldChunkAsync(chunksArray[0], cancellationToken);
                return 1;
            }

            // Read which chunks are held, and create the others again in halves.
            var heldHashes = await FindHeldHashesAsync(chunksArray.Select(c => c.Id).ToArray(), cancellationToken);
            var heldChunks = chunksArray.Where(c => heldHashes.Contains(c.Id)).ToArray();
            foreach (var heldChunk in heldChunks)
                await AddPinsToHeldChunkAsync(heldChunk, cancellationToken);

            var notHeldChunks = chunksArray.Where(c => !heldHashes.Contains(c.Id)).ToArray();
            if (notHeldChunks.Length == 0)
                return heldChunks.Length;
            var halfLength = (notHeldChunks.Length + 1) / 2;
            var heldInHalves = await CreateOrAddPinsAsync(notHeldChunks[..halfLength], cancellationToken);
            if (notHeldChunks.Length > halfLength)
                heldInHalves += await CreateOrAddPinsAsync(notHeldChunks[halfLength..], cancellationToken);
            return heldChunks.Length + heldInHalves;
        }

        // Protected methods.
        /// <summary>
        /// Add a pin to the document of a held chunk, unless it has it already
        /// </summary>
        protected virtual Task AddPinToHeldChunkAsync(
            SwarmHash hash,
            ChunkPin pin,
            CancellationToken cancellationToken) =>
            TryFindOneAndAddToSetAsync(
                new ExpressionFilterDefinition<Chunk>(c => c.Id == hash),
                c => c.Pins,
                pin,
                new FindOneAndUpdateOptions<Chunk>(),
                cancellationToken);

        /// <summary>
        /// The hashes among these that the db holds
        /// </summary>
        protected virtual async Task<HashSet<SwarmHash>> FindHeldHashesAsync(
            SwarmHash[] hashes,
            CancellationToken cancellationToken) =>
            (await QueryElementsAsync(elements =>
                elements.Where(c => hashes.Contains(c.Id))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken))).ToHashSet();

        /// <summary>
        /// The plain creation of the chunks, all or nothing: a hash held refuses the whole batch with a duplicate key
        /// </summary>
        protected virtual Task InsertChunksAsync(
            Chunk[] chunks,
            CancellationToken cancellationToken) =>
            base.CreateAsync(chunks, cancellationToken);

        // Helpers.
        private async Task AddPinsToHeldChunkAsync(
            Chunk heldChunk,
            CancellationToken cancellationToken)
        {
            foreach (var pin in heldChunk.Pins)
                await AddPinToHeldChunkAsync(heldChunk.Id, pin, cancellationToken);
        }

        private static bool IsDuplicateKeyFailure(Exception exception) =>
            exception switch
            {
                MongoWriteException e => e.WriteError.Category == ServerErrorCategory.DuplicateKey,
                MongoBulkWriteException e => e.WriteErrors.Any(w => w.Category == ServerErrorCategory.DuplicateKey),
                { InnerException: not null } => IsDuplicateKeyFailure(exception.InnerException),
                _ => false
            };
    }
}

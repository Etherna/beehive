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
using Etherna.MongoDB.Driver.Core.Clusters;
using Etherna.MongoDB.Driver.Core.Connections;
using Etherna.MongoDB.Driver.Core.Servers;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Persistence.Repositories
{
    public class SwarmChunksRepositoryTest
    {
        // Classes.
        /// <summary>
        /// The repository over a list, with the semantics of the db: an insert is all or nothing, as the transaction
        /// of the replica set, and a hash held already, or repeated in the batch, refuses the whole batch with the
        /// duplicate key of the unique id
        /// </summary>
        private sealed class ListSwarmChunksRepository() : SwarmChunksRepository(new RepositoryOptions<Chunk>("swarmChunks"))
        {
            // Properties.
            public List<Chunk> Chunks { get; } = [];
            public int HeldHashesReads { get; private set; }

            /// <summary>
            /// Run before a batch is inserted: a concurrent writer can create some of its chunks first
            /// </summary>
            public Action<Chunk[]>? OnInsertingChunks { get; set; }

            /// <summary>
            /// A failure of the db other than a duplicate key, refusing every insert
            /// </summary>
            public Exception? InsertFailure { get; set; }

            // Protected methods.
            protected override Task AddPinToHeldChunkAsync(SwarmHash hash, ChunkPin pin, CancellationToken cancellationToken)
            {
                var chunk = Chunks.Single(c => c.Id == hash);
                if (chunk.Pins.All(p => p.Id != pin.Id))
                    chunk.AddPin(pin);
                return Task.CompletedTask;
            }

            protected override Task<HashSet<SwarmHash>> FindHeldHashesAsync(SwarmHash[] hashes, CancellationToken cancellationToken)
            {
                HeldHashesReads++;
                return Task.FromResult(Chunks.Select(c => c.Id).Where(hashes.Contains).ToHashSet());
            }

            protected override Task InsertChunksAsync(Chunk[] chunks, CancellationToken cancellationToken)
            {
                if (InsertFailure is not null)
                    return Task.FromException(InsertFailure);
                OnInsertingChunks?.Invoke(chunks);
                if (chunks.Any(c => Chunks.Any(held => held.Id == c.Id)) ||
                    chunks.DistinctBy(c => c.Id).Count() < chunks.Length)
                {
                    // The driver's refusal of a document whose unique id is held: its write error is built by
                    // reflection, the driver keeps the constructor internal.
                    var writeError = (WriteError)Activator.CreateInstance(
                        typeof(WriteError),
                        BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        [ServerErrorCategory.DuplicateKey, 11000, "E11000 duplicate key error collection: swarmChunks", null],
                        null)!;
                    return Task.FromException(new MongoWriteException(
                        new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017))),
                        writeError,
                        null,
                        null));
                }
                Chunks.AddRange(chunks);
                return Task.CompletedTask;
            }
        }

        // Fields.
        private readonly ListSwarmChunksRepository repository = new();

        // Tests.

        [Fact]
        public async Task InsertsWithoutReadingTheDbWhenNoChunkIsHeld()
        {
            // Action.
            var held = await repository.CreateOrAddPinsAsync(Enumerable.Range(0, 16).Select(i => NewChunk(i)));

            // Assert.
            //the insert is optimistic: the db is read only after a refusal
            Assert.Equal(0, held);
            Assert.Equal(16, repository.Chunks.Count);
            Assert.Equal(0, repository.HeldHashesReads);
        }

        [Fact]
        public async Task AddsThePinsOfAChunkHeldAlreadyInsteadOfCreatingIt()
        {
            // Setup.
            var heldPin = NewPin();
            var createdPin = NewPin();
            var heldChunk = NewChunk(1, heldPin);
            repository.Chunks.Add(heldChunk);

            // Action.
            var held = await repository.CreateOrAddPinsAsync([NewChunk(1, createdPin), NewChunk(2, createdPin)]);

            // Assert.
            Assert.Equal(1, held);
            Assert.Equal(2, repository.Chunks.Count);
            Assert.Same(heldChunk, repository.Chunks.Single(c => c.Id == Hash(1)));
            Assert.Equal([heldPin, createdPin], heldChunk.Pins);
        }

        [Fact]
        public async Task CreatesTwoChunksOfTheSameHashInOneBatchAsOneWithBothPins()
        {
            // Setup.
            var pin1 = NewPin();
            var pin2 = NewPin();

            // Action.
            var held = await repository.CreateOrAddPinsAsync([NewChunk(1, pin1), NewChunk(1, pin2)]);

            // Assert.
            var chunk = Assert.Single(repository.Chunks);
            Assert.Equal(1, held);
            Assert.Equal([pin1, pin2], chunk.Pins);
        }

        [Fact]
        public async Task CreatesAContentWhileAConcurrentWriterKeepsCreatingSomeOfItsChunks()
        {
            // Setup.
            //another upload of the same content creates the first chunk of every batch tried, just before it
            var pin = NewPin();
            repository.OnInsertingChunks = batch =>
            {
                if (batch.Length > 1 && repository.Chunks.All(c => c.Id != batch[0].Id))
                    repository.Chunks.Add(new Chunk(batch[0].Id, batch[0].Payload, false));
            };

            // Action.
            await repository.CreateAsync(Enumerable.Range(0, 16).Select(i => NewChunk(i, pin)));

            // Assert.
            //every chunk once, every one with the pin: the refused ones take it on the document the other writer created
            Assert.Equal(16, repository.Chunks.Count);
            Assert.Equal(16, repository.Chunks.Select(c => c.Id).Distinct().Count());
            Assert.All(repository.Chunks, c => Assert.Equal([pin], c.Pins));
        }

        [Fact]
        public async Task CreatesASingleChunkHeldAlreadyAddingItsPins()
        {
            // Setup.
            var pin = NewPin();
            var heldChunk = NewChunk(1);
            repository.Chunks.Add(heldChunk);

            // Action.
            await repository.CreateAsync(NewChunk(1, pin));

            // Assert.
            //a single chunk refused is held: no read of the db
            Assert.Same(heldChunk, Assert.Single(repository.Chunks));
            Assert.Equal([pin], heldChunk.Pins);
            Assert.Equal(0, repository.HeldHashesReads);
        }

        [Fact]
        public async Task PropagatesAFailureThatIsNotADuplicateKey()
        {
            // Setup.
            var failure = new MongoException("db down");
            repository.InsertFailure = failure;

            // Action.
            var exception = await Assert.ThrowsAsync<MongoException>(() => repository.CreateAsync([NewChunk(1)]));

            // Assert.
            Assert.Same(failure, exception);
            Assert.Equal(0, repository.HeldHashesReads);
        }

        // Helpers.
        private static SwarmHash Hash(int seed) =>
            SwarmHash.FromString(seed.ToString("x64", System.Globalization.CultureInfo.InvariantCulture));

        private static Chunk NewChunk(int seed, params ChunkPin[] pins)
        {
            var chunk = new Chunk(Hash(seed), new byte[] { (byte)seed }, false);
            foreach (var pin in pins)
                chunk.AddPin(pin);
            return chunk;
        }

        private static ChunkPin NewPin()
        {
            var pin = new ChunkPin(new SwarmReference(Hash(0), null));
            ReflectionHelper.SetValue(pin, p => p.Id, Guid.NewGuid().ToString());
            return pin;
        }
    }
}

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
using Etherna.Beehive.Services.Helpers;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Utilities
{
    public class BeehiveChunkStoreTest
    {
        // Consts.
        private const string NodeAOverlay = "0000000000000000000000000000000000000000000000000000000000000001";
        private const string NodeBOverlay = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

        // Fields.
        private readonly Mock<IBeeNodeLiveManager> beeNodeLiveManagerMock = new();
        private readonly InMemoryBeehiveDb db = new();
        private readonly MongoException dbFailure = new("db down");
        private readonly CapturingLogger logger = new();
        private readonly FakeBeeNode nodeA = new("nodeA", NodeAOverlay);
        private readonly FakeBeeNode nodeB = new("nodeB", NodeBOverlay);

        // Constructor.
        public BeehiveChunkStoreTest()
        {
            beeNodeLiveManagerMock.Setup(m => m.HealthyNodes).Returns(() => [nodeA.Instance, nodeB.Instance]);
        }

        // Tests.

        [Theory]
        [InlineData(0)] //written at once
        [InlineData(1)] //flushed by the add that fills the buffer
        public async Task AddPropagatesTheRefusedWrite(int savingBufferLength)
        {
            // Setup.
            RefuseEveryWrite();
            await using var store = NewStore(savingBufferLength);

            // Action.
            var exception = await Assert.ThrowsAsync<MongoException>(() => store.AddAsync(NewChunk()));

            // Assert.
            Assert.Same(dbFailure, exception);
        }

        [Theory]
        [InlineData(0)] //written at once
        [InlineData(1)] //flushed by the add that fills the buffer
        public async Task AfterARefusedWriteTheStoreThrowsItAgainWithoutWriting(int savingBufferLength)
        {
            // Setup.
            RefuseEveryWrite();
            await using var store = NewStore(savingBufferLength);
            await Assert.ThrowsAsync<MongoException>(() => store.AddAsync(NewChunk()));

            // Action.
            var addException = await Assert.ThrowsAsync<MongoException>(() => store.AddAsync(NewChunk()));
            var flushException = await Assert.ThrowsAsync<MongoException>(() => store.FlushSaveAsync());

            // Assert.
            //the same failure, at once: the sdk pipeline feeds every remaining chunk before observing a failure,
            //a new db round trip per chunk would make a refused upload fail slow
            Assert.Same(dbFailure, addException);
            Assert.Same(dbFailure, flushException);
            db.SwarmChunksMock.Verify( //written at once or flushed, one refused write
                r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AfterARefusedDisposeFlushTheFinalizerDoesNotWriteAgain()
        {
            // Setup.
            RefuseEveryWrite();
            //a store whose first refused write is the flush of its dispose: the dispose throws before marking the store disposed
            await Assert.ThrowsAsync<MongoException>(() => AddAndDisposeAsync(savingBufferLength: 2));

            // Action.
            //the finalizer of the store left undisposed runs now
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Assert.
            //one refused write, the dispose's: the finalizer left the buffer alone, instead of a second failure on the finalizer thread
            db.SwarmChunksMock.Verify(
                r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AfterARefusedFlushTheChunkStaysReadableAndTheDisposeDoesNotWriteAgain()
        {
            // Setup.
            RefuseEveryWrite();
            var chunk = NewChunk();
            var store = NewStore(savingBufferLength: 1);
            await Assert.ThrowsAsync<MongoException>(() => store.AddAsync(chunk));

            // Action.
            var readBack = await store.GetAsync(chunk.Hash);
            await store.DisposeAsync();

            // Assert.
            //the chunk is served from the buffer it stayed in
            Assert.Equal(chunk.Hash, readBack.Hash);
            //one refused write, the one of the add: the dispose left the buffer alone
            db.SwarmChunksMock.Verify(
                r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DoesNotAskAnotherNodeForAChunkItsPartitionNodeDoesNotHold()
        {
            // Setup.
            //the other node holds the chunk, but the partition node already searched the network for it
            var chunk = NewChunk();
            await OtherNodeOf(chunk.Hash).Chunks.AddAsync(chunk);
            await using var store = NewStore();

            // Action.
            var result = await store.GetAsync([chunk.Hash]);

            // Assert.
            Assert.Empty(result);
            Assert.Contains(chunk.Hash, PartitionNodeOf(chunk.Hash).RequestedHashes);
            Assert.Empty(OtherNodeOf(chunk.Hash).RequestedHashes);
            Assert.Empty(db.SwarmChunks);
            var notFound = Assert.Single(logger.Events, e => e.EventName == nameof(Extensions.LoggerExtensions.ChunksNotFoundOnNode));
            Assert.Equal(LogLevel.Debug, notFound.Level);
            Assert.Contains(PartitionNodeOf(chunk.Hash).Instance.Id, notFound.Message, StringComparison.Ordinal);
            Assert.Contains(chunk.Hash.ToString(), notFound.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(logger.Events, e => e.Level >= LogLevel.Warning);
        }

        [Fact]
        public async Task PropagatesTheReadFailureOfANode()
        {
            // Setup.
            //the node of the partition fails every read
            var chunk = NewChunk();
            PartitionNodeOf(chunk.Hash).MissingChunkStatusCode = 500;
            await using var store = NewStore();

            // Action & assert.
            var exception = await Assert.ThrowsAsync<SwarmSdkApiException>(() => store.GetAsync([chunk.Hash]));
            Assert.Equal(500, exception.StatusCode);
            Assert.Empty(OtherNodeOf(chunk.Hash).RequestedHashes);
            Assert.DoesNotContain(logger.Events, e => e.Level >= LogLevel.Warning); //logged once, by who answers the failure
        }

        [Fact]
        public async Task ReadsAChunkFromItsPartitionNodeAndSavesIt()
        {
            // Setup.
            var chunk = NewChunk();
            await PartitionNodeOf(chunk.Hash).Chunks.AddAsync(chunk);
            await using var store = NewStore();

            // Action.
            var result = await store.GetAsync([chunk.Hash]);

            // Assert.
            Assert.Equal(chunk.Hash, Assert.Single(result).Value?.Hash);
            Assert.Empty(OtherNodeOf(chunk.Hash).RequestedHashes);
            Assert.Equal(chunk.Hash, Assert.Single(db.SwarmChunks).Id); //saved, as any chunk read from a node
            Assert.DoesNotContain(logger.Events, e => e.Level >= LogLevel.Warning);
        }

        [Fact]
        public async Task ReadsAChunkOfTheDeprecatedChunksWithoutAskingTheNodesNorCopyingIt()
        {
            // Setup.
            //a chunk of the chunks of 0.4.0, not migrated yet: a node holds it too
            var chunk = NewChunk();
            db.AddLegacyChunk(new LegacyChunk(chunk.Hash, chunk.SpanData, false));
            await PartitionNodeOf(chunk.Hash).Chunks.AddAsync(chunk);
            await using var store = NewStore();

            // Action.
            var single = await store.GetAsync(chunk.Hash);
            var batch = await store.GetAsync([chunk.Hash]);

            // Assert.
            Assert.Equal(chunk.SpanData.ToArray(), Assert.IsType<SwarmCac>(single).SpanData.ToArray());
            Assert.Equal(chunk.Hash, Assert.Single(batch).Value?.Hash);
            Assert.Empty(nodeA.RequestedHashes);
            Assert.Empty(nodeB.RequestedHashes);
            Assert.Empty(db.SwarmChunks); //moved by the migration, never by a read
        }

        [Fact]
        public async Task ReadsFromTheSourceNodeOnlyWhenOneIsGiven()
        {
            // Setup.
            var chunk = NewChunk();
            await nodeB.Chunks.AddAsync(chunk);
            await using var store = NewStore(sourceNode: nodeA);

            // Action.
            var result = await store.GetAsync([chunk.Hash]);

            // Assert.
            Assert.Empty(result);
            Assert.Contains(chunk.Hash, nodeA.RequestedHashes);
            Assert.Empty(nodeB.RequestedHashes);
        }

        [Fact]
        public async Task SavesAChunkHeldAlreadyOnceWithThePinsOfTheSavedOne()
        {
            // Setup.
            //the chunk is held with a pin, and saved again by an upload pinning it with another one
            var chunk = NewChunk();
            var heldPin = NewPin();
            var savedPin = NewPin();
            var heldDocument = new Chunk(chunk.Hash, chunk.SpanData, false);
            heldDocument.AddPin(heldPin);
            db.AddChunk(heldDocument);
            await using var store = NewStore(onSavingChunk: c => c.AddPin(savedPin));

            // Action.
            await store.AddAsync(chunk);
            await store.FlushSaveAsync();

            // Assert.
            var document = Assert.Single(db.SwarmChunks);
            Assert.Same(heldDocument, document);
            Assert.Equal([heldPin, savedPin], document.Pins);
        }

        // Helpers.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task AddAndDisposeAsync(int savingBufferLength)
        {
            var store = NewStore(savingBufferLength);
            await store.AddAsync(NewChunk()); //buffered, not written yet
            await store.DisposeAsync(); //writes the buffer, refused
        }

        /// <summary>
        /// A content addressed chunk, as an upload would build it
        /// </summary>
        private static SwarmCac NewChunk(int seed = 0)
        {
            var bmt = new SwarmChunkBmt();
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, Enumerable.Range(seed, 42).Select(i => (byte)i).ToArray()).SpanData;
            return new SwarmCac(bmt.Hash(spanData), spanData);
        }

        private ChunkPin NewPin()
        {
            var pin = new ChunkPin(new SwarmReference(NewChunk().Hash, null));
            db.AddPin(pin);
            return pin;
        }

        private BeehiveChunkStore NewStore(
            int savingBufferLength = BeehiveChunkStore.DefaultSavingBufferLength,
            FakeBeeNode? sourceNode = null,
            Action<Chunk>? onSavingChunk = null) =>
            new(
                beeNodeLiveManagerMock.Object,
                db.DbContext,
                logger,
                new Mock<ISerializerModifierAccessor>().Object,
                savingBufferLength,
                onSavingChunk,
                sourceNode?.Instance);

        private FakeBeeNode OtherNodeOf(SwarmHash hash) =>
            PartitionNodeOf(hash) == nodeA ? nodeB : nodeA;

        /// <summary>
        /// The node the store asks first: the healthy nodes ordered by overlay, each taking a bucket range
        /// </summary>
        private FakeBeeNode PartitionNodeOf(SwarmHash hash) =>
            (hash.ToBucketId() * 2u) >> 16 == 0 ? nodeA : nodeB;

        /// <summary>
        /// The db refuses every chunk write
        /// </summary>
        private void RefuseEveryWrite()
        {
            db.SwarmChunksMock.Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(dbFailure);
        }
    }
}

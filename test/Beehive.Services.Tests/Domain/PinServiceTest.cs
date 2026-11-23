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
using Etherna.Beehive.Services.Helpers;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Domain
{
    public class PinServiceTest
    {
        // Consts.
        private const string NodeId = "nodeA";
        private const string NodeOverlay = "aa00000000000000000000000000000000000000000000000000000000000001";

        // Fields.
        private readonly InMemoryBeehiveDb db = new();
        private readonly FakeBeeNode node = new(NodeId, NodeOverlay);
        private readonly List<FakeResourceLockLease> pinLeases = [];
        private readonly PinService service;

        // Constructor.
        public PinServiceTest()
        {
            // The node is the only healthy one: the nearest for every hash, and the whole partition.
            var beeNodeLiveManagerMock = new Mock<IBeeNodeLiveManager>();
            beeNodeLiveManagerMock.Setup(m => m.HealthyNodes).Returns(() => [node.Instance]);
            beeNodeLiveManagerMock.Setup(m => m.SelectNearestHealthyNode(It.IsAny<SwarmHash>())).Returns(node.Instance);

            // Every lock is granted, unless a test holds one.
            db.DbContextMock.Setup(c => c.TryAcquireResourceLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ResourceLockMode>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(() =>
                {
                    var lease = new FakeResourceLockLease();
                    pinLeases.Add(lease);
                    return lease;
                });

            service = new PinService(
                beeNodeLiveManagerMock.Object,
                db.DbContext,
                NullLogger<PinService>.Instance,
                new Mock<ISerializerModifierAccessor>().Object);
        }

        // Tests.

        [Fact]
        public async Task AcquireLockThrowsResourceLockExceptionWhenThePinIsHeld()
        {
            // Setup.
            //the exclusive lease of the pin is held by another operation: the db context denies it
            db.DbContextMock.Setup(c => c.TryAcquireResourceLockAsync(
                    ResourceLockNamespaces.ChunkPins,
                    "heldPin",
                    ResourceLockMode.Exclusive,
                    null))
                .ReturnsAsync((IResourceLockLease?)null);

            // Action & assert.
            var exception = await Assert.ThrowsAsync<ResourceLockException>(() => service.AcquireLockAsync("heldPin", true));
            Assert.Contains("heldPin", exception.Message, StringComparison.Ordinal); //the resource locked is named
        }

        [Fact]
        public async Task CountsAsMissingAChunkWhoseDocumentIsGoneWhenThePinIsAttached()
        {
            // Setup.
            //a data chunk document deleted between its fetch and the pinning: the add-to-set finds nothing
            var reference = await TestContents.UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);
            var goneHash = node.Chunks.AllChunks.Keys.First(h => h != reference.Hash);
            db.SwarmChunksMock.Setup(r => r.TryFindOneAndAddToSetAsync(
                    It.IsAny<FilterDefinition<Chunk>>(),
                    It.IsAny<Expression<Func<Chunk, IEnumerable<ChunkPin>>>>(),
                    It.IsAny<ChunkPin>(),
                    It.IsAny<FindOneAndUpdateOptions<Chunk>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    FilterDefinition<Chunk> filter,
                    Expression<Func<Chunk, IEnumerable<ChunkPin>>> _,
                    ChunkPin addedPin,
                    FindOneAndUpdateOptions<Chunk> _,
                    CancellationToken _) =>
                {
                    var chunk = db.SwarmChunks.FirstOrDefault(((ExpressionFilterDefinition<Chunk>)filter).Expression.Compile());
                    if (chunk is null)
                        return Task.FromResult<Chunk?>(null);
                    if (chunk.Id == goneHash)
                    {
                        db.SwarmChunks.Remove(chunk);
                        return Task.FromResult<Chunk?>(null);
                    }
                    if (chunk.Pins.All(p => p.Id != addedPin.Id))
                        chunk.AddPin(addedPin);
                    return Task.FromResult<Chunk?>(chunk);
                });

            // Action.
            var result = await service.PinChunksAsync(pin.Id, node.Instance);

            // Assert.
            Assert.False(result.Pin.IsSucceeded);
            Assert.Equal([goneHash], result.Pin.MissingChunks);
            Assert.Equal(node.Chunks.AllChunks.Count - 1, result.Pin.TotPinnedChunks);
            Assert.DoesNotContain(db.SwarmChunks, c => c.Id == goneHash);
            Assert.All(db.SwarmChunks, c => Assert.Equal([pin], c.Pins));
        }

        [Fact]
        public async Task FailsThePinWithTheChunksWhoseDocumentDidNotTakeThePin()
        {
            // Setup.
            //beehive holds the content already, and the add-to-set answers the documents without writing the pin
            var reference = await TestContents.UploadContentAsync(node.Chunks);
            foreach (var chunk in node.Chunks.AllChunks.Values)
                db.AddChunk(new Chunk(chunk.Hash, chunk.GetFullPayload(), false));
            var pin = NewPin(reference);
            db.SwarmChunksMock.Setup(r => r.TryFindOneAndAddToSetAsync(
                    It.IsAny<FilterDefinition<Chunk>>(),
                    It.IsAny<Expression<Func<Chunk, IEnumerable<ChunkPin>>>>(),
                    It.IsAny<ChunkPin>(),
                    It.IsAny<FindOneAndUpdateOptions<Chunk>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    FilterDefinition<Chunk> filter,
                    Expression<Func<Chunk, IEnumerable<ChunkPin>>> _,
                    ChunkPin _,
                    FindOneAndUpdateOptions<Chunk> _,
                    CancellationToken _) =>
                    Task.FromResult(db.SwarmChunks.FirstOrDefault(((ExpressionFilterDefinition<Chunk>)filter).Expression.Compile())));

            // Action.
            var result = await service.PinChunksAsync(pin.Id, node.Instance);

            // Assert.
            Assert.False(result.Pin.IsSucceeded);
            Assert.Equal(0, result.Pin.TotPinnedChunks);
            Assert.Equal(node.Chunks.AllChunks.Count, result.Pin.MissingChunks.Count());
            Assert.Empty(node.RequestedHashes); //nothing was read from the node
        }

        [Fact]
        public async Task PinChunksCancelledReleasesThePinLockAndLeavesThePinNotProcessed()
        {
            // Setup.
            //the shutdown arrives while the chunks are read from the node: the token reaches the reads, which stop
            var reference = await TestContents.UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);
            using var shutdown = new CancellationTokenSource();
            await shutdown.CancelAsync();

            // Action.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PinChunksAsync(pin.Id, node.Instance, shutdown.Token));

            // Assert.
            Assert.True(Assert.Single(pinLeases).IsReleased);
            Assert.False(pin.IsProcessed);
            db.DbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PinChunksDoesNotDeclareAPinWhoseLockWasLostBeforeItsResult()
        {
            // Setup.
            //the lease is lost when the traversal is over, while the pinned documents are read back
            var reference = await TestContents.UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);
            db.SwarmChunksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<Chunk>, Task<List<SwarmHash>>>>(),
                    null))
                .Returns((Func<IQueryable<Chunk>, Task<List<SwarmHash>>> query, AggregateOptions? _) =>
                {
                    pinLeases.Single().Lose();
                    return query(InMemoryQueryable.Of(db.SwarmChunks));
                });

            // Action.
            var exception = await Assert.ThrowsAsync<ResourceLockException>(() => service.PinChunksAsync(pin.Id, node.Instance));

            // Assert.
            Assert.Contains(pin.Id, exception.Message, StringComparison.Ordinal);
            Assert.True(Assert.Single(pinLeases).IsReleased);
            Assert.False(pin.IsProcessed);
            db.DbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PinChunksReleasesThePinLockWhenItFails()
        {
            // Action.
            //a pin that can't be loaded fails after its lock was taken
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.PinChunksAsync("missingPin", null));

            // Assert.
            Assert.True(Assert.Single(pinLeases).IsReleased);
        }

        [Fact]
        public async Task PinChunksStopsReadingTheNodeWhenThePinLockIsLost()
        {
            // Setup.
            //the lease is lost once the pin is loaded: its token reaches the reads of the node, which stop
            var reference = await TestContents.UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);
            db.ChunkPinsMock.Setup(r => r.FindOneAsync(pin.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    pinLeases.Single().Lose();
                    return pin;
                });

            // Action.
            var exception = await Assert.ThrowsAsync<ResourceLockException>(() => service.PinChunksAsync(pin.Id, node.Instance));

            // Assert.
            Assert.Contains(pin.Id, exception.Message, StringComparison.Ordinal);
            Assert.True(Assert.Single(pinLeases).IsReleased);
            Assert.False(pin.IsProcessed);
            Assert.Empty(node.RequestedHashes); //no read was sent after the loss
            Assert.Empty(db.SwarmChunks);
            db.DbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PinsAContentReadFromTheHealthyNodesWithoutASourceNode()
        {
            // Setup.
            var reference = await TestContents.UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);

            // Action.
            var result = await service.PinChunksAsync(pin.Id, null);

            // Assert.
            AssertContentPinned(result, pin);
        }

        [Fact]
        public async Task PinsAContentReadFromTheSourceNodeSavingEveryChunkWithThePin()
        {
            // Setup.
            var reference = await TestContents.UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);

            // Action.
            var result = await service.PinChunksAsync(pin.Id, node.Instance);

            // Assert.
            AssertContentPinned(result, pin);
        }

        [Fact]
        public async Task PinsAChunkStillInTheDeprecatedChunksOnItsDocument()
        {
            // Setup.
            //beehive holds the content, one data chunk of it in the chunks of 0.4.0 only, not migrated yet
            var reference = await TestContents.UploadContentAsync(node.Chunks);
            var legacyChunk = node.Chunks.AllChunks.Values.First(c => c.Hash != reference.Hash);
            foreach (var chunk in node.Chunks.AllChunks.Values.Where(c => c.Hash != legacyChunk.Hash))
                db.AddChunk(new Chunk(chunk.Hash, chunk.GetFullPayload(), false));
            var legacyDocument = new LegacyChunk(legacyChunk.Hash, legacyChunk.GetFullPayload(), false);
            db.AddLegacyChunk(legacyDocument);
            var pin = NewPin(reference);

            // Action.
            var result = await service.PinChunksAsync(pin.Id, node.Instance);

            // Assert.
            Assert.True(result.Pin.IsSucceeded);
            Assert.Equal(node.Chunks.AllChunks.Count, result.Pin.TotPinnedChunks);
            Assert.Equal([pin], legacyDocument.Pins); //its migration moves the pin
            Assert.DoesNotContain(db.SwarmChunks, c => c.Id == legacyChunk.Hash);
            Assert.Empty(node.RequestedHashes); //nothing was read from the node
        }

        [Fact]
        public async Task PinsASingleOwnerChunkReferenceAsItsSingleChunk()
        {
            // Setup.
            //a soc pinned on the node, nothing in the db: as bee, the wrapped chunk is not entered
            var soc = TestContents.NewSignedSoc();
            await node.Chunks.AddAsync(soc);
            var pin = NewPin(new SwarmReference(soc.Hash, null));

            // Action.
            var result = await service.PinChunksAsync(pin.Id, node.Instance);

            // Assert.
            Assert.True(result.Pin.IsSucceeded);
            Assert.Equal(1, result.Pin.TotPinnedChunks);
            Assert.Empty(result.InvalidChunks);
            var chunk = Assert.Single(db.SwarmChunks);
            Assert.Equal(soc.Hash, chunk.Id);
            Assert.True(chunk.IsSoc);
            Assert.Contains(pin, chunk.Pins);
        }

        // Helpers.
        private void AssertContentPinned(Utilities.Models.PinChunksResult result, ChunkPin pin)
        {
            var contentHashes = node.Chunks.AllChunks.Keys.ToHashSet();
            Assert.True(contentHashes.Count > 130); //a three level content, not a tiny one

            Assert.Same(pin, result.Pin);
            Assert.True(result.Pin.IsSucceeded);
            Assert.Empty(result.InvalidChunks);
            Assert.Equal(contentHashes.Count, result.Pin.TotPinnedChunks);

            //every chunk of the content is on the db, once, with the pin once
            Assert.Equal(contentHashes.Count, db.SwarmChunks.Count);
            Assert.Equal(contentHashes, db.SwarmChunks.Select(c => c.Id).ToHashSet());
            Assert.All(db.SwarmChunks, c => Assert.Equal([pin], c.Pins));
        }

        private ChunkPin NewPin(SwarmReference reference)
        {
            var pin = new ChunkPin(reference);
            db.AddPin(pin);
            return pin;
        }
    }
}

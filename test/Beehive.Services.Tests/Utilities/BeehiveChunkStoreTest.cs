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
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
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
        // Fields.
        private readonly InMemoryBeehiveDb db = new();
        private readonly MongoException dbFailure = new("db down");

        // Constructor.
        public BeehiveChunkStoreTest()
        {
            // The db refuses every chunk write.
            db.ChunksMock.Setup(r => r.CreateAsync(It.IsAny<Chunk>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(dbFailure);
            db.ChunksMock.Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(dbFailure);
        }

        // Tests.

        [Theory]
        [InlineData(0)] //written at once
        [InlineData(1)] //flushed by the add that fills the buffer
        public async Task AddPropagatesTheRefusedWrite(int savingBufferLength)
        {
            // Setup.
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
            db.ChunksMock.Verify(
                r => r.CreateAsync(It.IsAny<Chunk>(), It.IsAny<CancellationToken>()),
                Times.Exactly(savingBufferLength == 0 ? 1 : 0));
            db.ChunksMock.Verify(
                r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()),
                Times.Exactly(savingBufferLength == 0 ? 0 : 1));
        }

        [Fact]
        public async Task AfterARefusedDisposeFlushTheFinalizerDoesNotWriteAgain()
        {
            // Setup.
            //a store whose first refused write is the flush of its dispose: the dispose throws before marking the store disposed
            await Assert.ThrowsAsync<MongoException>(() => AddAndDisposeAsync(savingBufferLength: 2));

            // Action.
            //the finalizer of the store left undisposed runs now
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Assert.
            //one refused write, the dispose's: the finalizer left the buffer alone, instead of a second failure on the finalizer thread
            db.ChunksMock.Verify(
                r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task AfterARefusedFlushTheChunkStaysReadableAndTheDisposeDoesNotWriteAgain()
        {
            // Setup.
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
            db.ChunksMock.Verify(
                r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        // Helpers.
        private static SwarmCac NewChunk()
        {
            var bmt = new SwarmChunkBmt();
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, Enumerable.Range(0, 42).Select(i => (byte)i).ToArray()).SpanData;
            return new SwarmCac(bmt.Hash(spanData), spanData);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task AddAndDisposeAsync(int savingBufferLength)
        {
            var store = NewStore(savingBufferLength);
            await store.AddAsync(NewChunk()); //buffered, not written yet
            await store.DisposeAsync(); //writes the buffer, refused
        }

        private BeehiveChunkStore NewStore(int savingBufferLength) =>
            new(
                new Mock<IBeeNodeLiveManager>().Object,
                db.DbContext,
                NullLogger.Instance,
                new Mock<ISerializerModifierAccessor>().Object,
                savingBufferLength);
    }
}

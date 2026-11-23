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
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Domain
{
    public class PinIntegrityServiceTest
    {
        // Consts.
        private const string NodeId = "nodeA";
        private const string NodeOverlay = "aa00000000000000000000000000000000000000000000000000000000000001";

        // Fields.
        private readonly InMemoryBeehiveDb db = new();
        private readonly FakeDeprecatedChunkStores deprecatedChunkStores = new();
        private readonly FakeBeeNode node = new(NodeId, NodeOverlay);
        private readonly List<FakeResourceLockLease> pinLeases = [];
        private readonly PinIntegrityService service;

        // Constructor.
        public PinIntegrityServiceTest()
        {
            // The node is the only healthy one, holding every content: the check must never ask it.
            var beeNodeLiveManagerMock = new Mock<IBeeNodeLiveManager>();
            beeNodeLiveManagerMock.Setup(m => m.HealthyNodes).Returns(() => [node.Instance]);
            beeNodeLiveManagerMock.Setup(m => m.SelectNearestHealthyNode(It.IsAny<SwarmHash>())).Returns(node.Instance);

            var pinServiceMock = new Mock<IPinService>();
            pinServiceMock.Setup(s => s.AcquireLockAsync(It.IsAny<string>(), true))
                .ReturnsAsync(() =>
                {
                    var lease = new FakeResourceLockLease();
                    pinLeases.Add(lease);
                    return lease;
                });

            service = new PinIntegrityService(
                beeNodeLiveManagerMock.Object,
                db.DbContext,
                deprecatedChunkStores,
                NullLogger<PinIntegrityService>.Instance,
                pinServiceMock.Object,
                new Mock<ISerializerModifierAccessor>().Object);
        }

        // Tests.

        [Fact]
        public async Task AttachesThePinToAChunkHeldWithoutIt()
        {
            // Setup.
            var (pin, _) = await NewPinnedContentAsync();
            //a data chunk document held without the pin among its references
            var pinnedChunk = db.SwarmChunks.First(c => IsDataChunk(c.Payload));
            db.SwarmChunks.Remove(pinnedChunk);
            var unreferencedChunk = NewChunkDocument(pinnedChunk.Id, pinnedChunk.Payload.ToArray(), false, null);
            db.AddChunk(unreferencedChunk);

            // Action.
            var result = await service.CheckPinAsync(pin);

            // Assert.
            Assert.True(result.IsComplete);
            Assert.Equal(1, result.ReferencedChunks);
            Assert.Equal([pin], unreferencedChunk.Pins);
        }

        [Fact]
        public async Task CountsAsInvalidAChunkWhosePayloadIsNotTheChunkAtItsAddress()
        {
            // Setup.
            //a data chunk document whose payload has one byte changed: same length, another hash
            var (pin, contentHashes) = await NewPinnedContentAsync();
            var corruptChunk = db.SwarmChunks.First(c => IsDataChunk(c.Payload));
            db.SwarmChunks.Remove(corruptChunk);
            var corruptPayload = corruptChunk.Payload.ToArray();
            corruptPayload[^1] ^= 0xff;
            db.AddChunk(NewChunkDocument(corruptChunk.Id, corruptPayload, false, pin));

            // Action.
            var result = await service.CheckPinAsync(pin);

            // Assert.
            Assert.False(result.IsComplete);
            Assert.Equal([corruptChunk.Id], result.InvalidChunks);
            Assert.Empty(result.MissingChunks);
            Assert.Equal(contentHashes.Length, result.TotalChunks);
        }

        [Fact]
        public async Task CountsAsInvalidAChunkThatIsNotANodeOfTheContentTreeAndStopsAtIt()
        {
            // Setup.
            //a root that is the chunk at its address, but whose span says more than a chunk holds while its data is
            //not a list of references
            var payload = new byte[SwarmCac.SpanSize + 40];
            BitConverter.GetBytes(8192UL).CopyTo(payload, 0);
            var hash = new SwarmChunkBmt().Hash(payload);
            var pin = NewPin(new SwarmReference(hash, null));
            db.AddChunk(NewChunkDocument(hash, payload, false, pin));

            // Action.
            var result = await service.CheckPinAsync(pin);

            // Assert.
            Assert.False(result.IsComplete);
            Assert.Equal([hash], result.InvalidChunks);
            Assert.Equal(hash, result.MalformedChunk);
            Assert.Empty(result.MissingChunks);
            Assert.Equal(1, result.TotalChunks);
        }

        [Fact]
        public async Task CountsAsInvalidASingleOwnerChunkRootThatDoesNotVerify()
        {
            // Setup.
            //the document of a soc whose wrapped data has one byte changed: its signature no longer verifies
            var soc = TestContents.NewSignedSoc();
            var corruptPayload = soc.GetFullPayloadToByteArray();
            corruptPayload[^1] ^= 0xff;
            var pin = NewPin(new SwarmReference(soc.Hash, null));
            db.AddChunk(NewChunkDocument(soc.Hash, corruptPayload, true, pin));

            // Action.
            var result = await service.CheckPinAsync(pin);

            // Assert.
            Assert.Equal(1, result.TotalChunks);
            Assert.Equal([soc.Hash], result.InvalidChunks);
            Assert.Empty(result.MissingChunks);
        }

        [Fact]
        public async Task CountsAsMissingAChunkHeldOnlyInTheDeprecatedChunks()
        {
            // Setup.
            //a data chunk held by a document of the deprecated chunks: the check reads the chunks keyed by hash only
            var (pin, contentHashes) = await NewPinnedContentAsync();
            var movedChunk = db.SwarmChunks.First(c => IsDataChunk(c.Payload));
            db.SwarmChunks.Remove(movedChunk);
            var legacyDocument = new LegacyChunk(movedChunk.Id, movedChunk.Payload, false);
            db.AddLegacyChunk(legacyDocument);

            // Action.
            var result = await service.CheckPinAsync(pin);

            // Assert.
            Assert.False(result.IsComplete);
            Assert.Equal([movedChunk.Id], result.MissingChunks);
            Assert.Equal(contentHashes.Length, result.TotalChunks);
            Assert.Empty(legacyDocument.Pins); //the deprecated document is left as it is
        }

        [Fact]
        public async Task CountsAsMissingAChunkOnlyTheNodesHoldWithoutAskingThem()
        {
            // Setup.
            var (pin, contentHashes) = await NewPinnedContentAsync();
            var goneChunk = db.SwarmChunks.First(c => IsDataChunk(c.Payload));
            db.SwarmChunks.Remove(goneChunk);

            // Action.
            var result = await service.CheckPinAsync(pin);

            // Assert.
            Assert.False(result.IsComplete);
            Assert.Equal([goneChunk.Id], result.MissingChunks);
            Assert.Empty(result.InvalidChunks);
            Assert.Equal(contentHashes.Length, result.TotalChunks);
            Assert.Empty(node.RequestedHashes);
            Assert.DoesNotContain(db.SwarmChunks, c => c.Id == goneChunk.Id); //nothing was read and saved
        }

        [Fact]
        public async Task FindsCompleteAContentBeehiveHoldsInFull()
        {
            // Setup.
            var (pin, contentHashes) = await NewPinnedContentAsync();

            // Action.
            var result = await service.CheckPinAsync(pin);

            // Assert.
            Assert.True(contentHashes.Length > 130); //a three level content, not a tiny one
            Assert.True(result.IsComplete);
            Assert.Equal(pin.Reference, result.Reference);
            Assert.Equal(contentHashes.Length, result.TotalChunks);
            Assert.Equal(0, result.ReferencedChunks);
            Assert.Empty(node.RequestedHashes);
        }

        [Fact]
        public async Task ResetPinDeletesTheInvalidChunksAndLeavesThePinNotSucceeded()
        {
            // Setup.
            var (pin, contentHashes) = await NewPinnedContentAsync();
            var dataChunks = db.SwarmChunks.Where(c => IsDataChunk(c.Payload)).Take(2).ToArray();
            var missingHash = dataChunks[0].Id;
            var invalidHash = dataChunks[1].Id;
            db.SwarmChunks.Remove(dataChunks[0]);
            var checkResult = new PinCheckResult(pin.Reference!.Value, contentHashes.Length, [missingHash], [invalidHash], 0);

            // Action.
            var resetPin = await service.ResetPinAsync(pin.Id, checkResult);

            // Assert.
            Assert.Same(pin, resetPin);
            Assert.False(pin.IsSucceeded);
            Assert.Equal(new HashSet<SwarmHash> { missingHash, invalidHash }, pin.MissingChunks.ToHashSet());
            Assert.Equal(contentHashes.Length - 2, pin.TotPinnedChunks);
            Assert.DoesNotContain(db.SwarmChunks, c => c.Id == invalidHash); //a new pinning reads it from the nodes
            Assert.Equal(contentHashes.Length - 2, db.SwarmChunks.Count);
            Assert.True(Assert.Single(pinLeases).IsReleased);
        }

        [Fact]
        public async Task ResetPinKeepsTheMalformedChunk()
        {
            // Setup.
            //a chunk of the content stands for the malformed one: its payload is the chunk at its address
            var (pin, contentHashes) = await NewPinnedContentAsync();
            var malformedHash = db.SwarmChunks.First(c => IsDataChunk(c.Payload)).Id;
            var checkResult = new PinCheckResult(
                pin.Reference!.Value, contentHashes.Length, [], [malformedHash], 0, malformedHash);

            // Action.
            await service.ResetPinAsync(pin.Id, checkResult);

            // Assert.
            Assert.False(pin.IsSucceeded);
            Assert.Equal([malformedHash], pin.MissingChunks);
            Assert.Contains(db.SwarmChunks, c => c.Id == malformedHash); //a node would serve the same payload
            Assert.Equal(contentHashes.Length, db.SwarmChunks.Count);
        }

        [Fact]
        public async Task VerifiesThatThePinsCanBeCheckedOnlyWhileTheDeprecatedStoresAreEmpty()
        {
            // Action and assert.
            await service.VerifyPinsCanBeCheckedAsync();

            //a chunk still in the deprecated chunks collection: a check would count it missing
            var legacyChunk = new FakeDeprecatedChunkStores.StoredLegacyChunk(
                "000000000000000000000001", SwarmHash.Zero, [1], false, DateTime.UtcNow);
            deprecatedChunkStores.LegacyChunks.Add(legacyChunk);
            await Assert.ThrowsAsync<DeprecatedChunksNotMigratedException>(() => service.VerifyPinsCanBeCheckedAsync());
            deprecatedChunkStores.LegacyChunks.Remove(legacyChunk);

            //a chunk still in the deprecated GridFS bucket
            deprecatedChunkStores.GridFsFiles.Add(new FakeDeprecatedChunkStores.StoredGridFsFile(
                MongoDB.Bson.ObjectId.GenerateNewId(), "f", [1], DateTime.UtcNow));
            await Assert.ThrowsAsync<DeprecatedChunksNotMigratedException>(() => service.VerifyPinsCanBeCheckedAsync());
        }

        // Helpers.
        private static bool IsDataChunk(ReadOnlyMemory<byte> payload) =>
            SwarmCac.SpanToLength(payload.Span[..SwarmCac.SpanSize]) <= SwarmCac.DataSize;

        private static Chunk NewChunkDocument(SwarmHash hash, byte[] payload, bool isSoc, ChunkPin? pin)
        {
            var chunk = new Chunk(hash, payload, isSoc);
            if (pin is not null)
                chunk.AddPin(pin);
            return chunk;
        }

        private ChunkPin NewPin(SwarmReference reference)
        {
            var pin = new ChunkPin(reference);
            db.AddPin(pin);
            return pin;
        }

        /// <summary>
        /// A content uploaded to the node and held by beehive in full, every chunk document carrying the pin,
        /// declared succeeded
        /// </summary>
        private async Task<(ChunkPin Pin, SwarmHash[] ContentHashes)> NewPinnedContentAsync()
        {
            var reference = await TestContents.UploadContentAsync(node.Chunks);
            var pin = NewPin(reference);
            foreach (var chunk in node.Chunks.AllChunks.Values)
                db.AddChunk(NewChunkDocument(chunk.Hash, chunk.GetFullPayloadToByteArray(), false, pin));
            pin.UpdateProcessed([], node.Chunks.AllChunks.Count);
            return (pin, [.. node.Chunks.AllChunks.Keys]);
        }
    }
}

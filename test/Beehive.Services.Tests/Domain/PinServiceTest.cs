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
using Etherna.Beehive.Services.Utilities;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Domain
{
    public class PinServiceTest
    {
        // Consts.
        private const string NodeId = "nodeA";
        private const string PrivateKeyHex = "0x4f3edf983ac636a65a842ce7c78d9aa706d3b113bce9c46f30d7d21715b23b1d";

        // Fields.
        private readonly InMemoryBeehiveDb db = new();
        private readonly PinService service;

        // Constructor.
        public PinServiceTest()
        {
            // The pin lock is free.
            var resourceLockServiceMock = new Mock<IResourceLockService>();
            resourceLockServiceMock.Setup(s => s.TryAcquireLockAsync(
                    It.IsAny<System.Func<ChunkPinLock>>(),
                    It.IsAny<IRepository<ChunkPinLock, string>>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()))
                .ReturnsAsync((System.Func<ChunkPinLock> _, IRepository<ChunkPinLock, string> repository, string resourceId, bool exclusiveAccess) =>
                    new ResourceLockHandler<ChunkPinLock>(repository, resourceLockServiceMock.Object, resourceId, exclusiveAccess)); //disposed by the pinning

            service = new PinService(
                db.DbContext,
                NullLogger<PinService>.Instance,
                resourceLockServiceMock.Object,
                new Mock<ISerializerModifierAccessor>().Object);
        }

        // Tests.

        [Fact]
        public async Task PinsASingleOwnerChunkReferenceAsItsSingleChunk()
        {
            // Setup.
            //a soc pinned on the node, nothing in the db: as bee, the wrapped chunk is not entered
            var soc = NewSignedSoc();
            var node = new FakeBeeNode(NodeId, soc);
            var pin = new ChunkPin(new SwarmReference(soc.Hash, null));
            await db.DbContext.ChunkPins.CreateAsync(pin);
            await using var chunkStore = new BeehiveChunkStore(
                new Mock<IBeeNodeLiveManager>().Object,
                db.DbContext,
                NullLogger.Instance,
                new Mock<ISerializerModifierAccessor>().Object,
                sourceNode: node.Node);

            // Action.
            var result = await service.PinChunksAsync(pin.Id, chunkStore);

            // Assert.
            Assert.True(result.Pin.IsSucceeded);
            Assert.Equal(1, result.Pin.TotPinnedChunks);
            Assert.Empty(result.InvalidChunks);
            var chunk = Assert.Single(db.Chunks);
            Assert.Equal(soc.Hash, chunk.Hash);
            Assert.True(chunk.IsSoc);
            Assert.Contains(pin, chunk.Pins);
        }

        // Helpers.
        private static SwarmSoc NewSignedSoc()
        {
            var bmt = new SwarmChunkBmt();
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, Enumerable.Range(0, 42).Select(i => (byte)i).ToArray()).SpanData;
            var innerChunk = new SwarmCac(bmt.Hash(spanData), spanData);
            var signer = new PrivateKeySigner(new EthPrivateKey(PrivateKeyHex));
            SwarmSocIdentifier identifier = Enumerable.Range(0, SwarmSocIdentifier.IdentifierSize).Select(i => (byte)i).ToArray();
            var soc = new SwarmSoc(identifier, signer.PublicAddress, innerChunk);
            soc.Sign(signer, new Hasher());
            return soc;
        }
    }
}

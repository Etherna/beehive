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
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Domain
{
    public class DataServiceTest
    {
        // Consts.
        private static readonly PostageBatchId UnknownBatchId = "aa00000000000000000000000000000000000000000000000000000000000001";

        // Fields.
        private readonly DataService service;

        // Constructor.
        public DataServiceTest()
        {
            // A batch cache knowing no batch.
            var postageBatchesCacheMock = new Mock<IRepository<PostageBatchCache, string>>();
            postageBatchesCacheMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<PostageBatchCache, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((PostageBatchCache?)null);
            var dbContextMock = new Mock<IBeehiveDbContext>();
            dbContextMock.Setup(c => c.PostageBatchesCache).Returns(postageBatchesCacheMock.Object);

            // The batch lock is granted: the lookup runs inside it.
            var postageBatchServiceMock = new Mock<IPostageBatchService>();
            postageBatchServiceMock.Setup(s => s.AcquireLockAsync(It.IsAny<PostageBatchId>(), It.IsAny<bool>()))
                .ReturnsAsync((PostageBatchId batchId, bool exclusiveAccess) => new ResourceLockHandler<PostageBatchLock>(
                    new Mock<IRepository<PostageBatchLock, string>>().Object,
                    new Mock<IResourceLockService>().Object,
                    batchId.ToString(),
                    exclusiveAccess));

            service = new DataService(
                new Mock<IBeeNodeLiveManager>().Object,
                dbContextMock.Object,
                NullLogger<DataService>.Instance,
                postageBatchServiceMock.Object,
                new Mock<ISerializerModifierAccessor>().Object);
        }

        // Tests.

        [Fact]
        public async Task UploadOnABatchUnknownToBeehiveIsRefusedAsBatchNotFound()
        {
            // Setup.
            var chunkingInvoked = false;

            // Action.
            var exception = await Assert.ThrowsAsync<PostageBatchNotFoundException>(() => service.UploadAsync(
                UnknownBatchId,
                null,
                false,
                false,
                (_, _) =>
                {
                    chunkingInvoked = true;
                    return Task.FromResult(new SwarmReference(SwarmHash.Zero, null));
                }));

            // Assert.
            Assert.Contains(UnknownBatchId.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.False(chunkingInvoked); //refused before anything is stored
        }
    }
}

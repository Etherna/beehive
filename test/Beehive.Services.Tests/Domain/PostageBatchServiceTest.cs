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
using Etherna.Beehive.Services.Helpers;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core.Repositories;
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Domain
{
    public class PostageBatchServiceTest
    {
        // Consts.
        private const string NodeAId = "nodeA";
        private const string NodeBId = "nodeB";
        private static readonly PostageBatchId BatchOnNodeAId = "aa00000000000000000000000000000000000000000000000000000000000001";
        private static readonly PostageBatchId BatchOnNodeBId = "bb00000000000000000000000000000000000000000000000000000000000002";
        private const string SyncingErrorBody = """{"code":503,"message":"syncing in progress"}""";

        // Fields.
        private readonly Mock<IBeeNodeLiveManager> beeNodeLiveManagerMock = new();
        private readonly Mock<ISwarmClient> nodeAClientMock = new();
        private readonly BeeNodeLiveInstance nodeA;
        private readonly Mock<ISwarmClient> nodeBClientMock = new();
        private readonly BeeNodeLiveInstance nodeB;
        private readonly List<PostageBatchCache> postageBatchesCache = [];
        private readonly PostageBatchService service;

        // Constructor.
        public PostageBatchServiceTest()
        {
            // Two registered nodes, both alive unless a test says otherwise.
            nodeA = new BeeNodeLiveInstance(NodeAId, nodeAClientMock.Object, true);
            nodeA.Status.SucceededHeartbeatAttempt(DateTime.UtcNow);
            nodeB = new BeeNodeLiveInstance(NodeBId, nodeBClientMock.Object, true);
            nodeB.Status.SucceededHeartbeatAttempt(DateTime.UtcNow);
            beeNodeLiveManagerMock.Setup(m => m.AllNodes).Returns(() => [nodeA, nodeB]);

            // A batch cache in memory: the lookups run their predicates and queries over it.
            var postageBatchesCacheMock = new Mock<IRepository<PostageBatchCache, string>>();
            postageBatchesCacheMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<PostageBatchCache, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<PostageBatchCache, bool>> predicate, CancellationToken _) =>
                    postageBatchesCache.FirstOrDefault(predicate.Compile()));
            postageBatchesCacheMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<PostageBatchCache>, Task<List<PostageBatchCache>>>>(),
                    null))
                .Returns((Func<IQueryable<PostageBatchCache>, Task<List<PostageBatchCache>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(postageBatchesCache)));
            var dbContextMock = new Mock<IBeehiveDbContext>();
            dbContextMock.Setup(c => c.PostageBatchesCache).Returns(postageBatchesCacheMock.Object);

            service = new PostageBatchService(
                beeNodeLiveManagerMock.Object,
                dbContextMock.Object,
                new Mock<IResourceLockService>().Object);
        }

        // Test data.
        public static IEnumerable<object[]> NodeFailureTests
        {
            get
            {
                var tests = new List<(Exception Exception, string ExpectedError)>
                {
                    //the node answered an error: its own reason
                    (NewApiException(503, SyncingErrorBody), "syncing in progress"),
                    (NewApiException(500, """{"code":500,"message":"get issuer failed"}"""), "get issuer failed"),

                    //the node did not answer
                    (new HttpRequestException("Connection refused (nodeB:1633)"), "Connection refused (nodeB:1633)")
                };

                return tests.Select(t => new object[] { t.Exception, t.ExpectedError });
            }
        }

        // Tests.

        [Fact]
        public async Task GetOwnedPostageBatchesReportsBatchesOfNotAliveNodeFromCache()
        {
            // Setup.
            nodeB.Status.FailedHeartbeatAttempt(["Node is not ready"], DateTime.UtcNow);
            nodeAClientMock.Setup(c => c.GetOwnedPostageBatchesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([NewLivePostageBatch(BatchOnNodeAId, isUsable: true)]);
            postageBatchesCache.Add(NewPostageBatchCache(BatchOnNodeAId, NodeAId));
            postageBatchesCache.Add(NewPostageBatchCache(BatchOnNodeBId, NodeBId));

            // Action.
            var result = (await service.GetOwnedPostageBatchesAsync()).ToArray();

            // Assert.
            Assert.Equal(2, result.Length);
            var liveBatch = Assert.Single(result, b => b.BatchId == BatchOnNodeAId);
            Assert.Null(liveBatch.Error);
            Assert.True(liveBatch.IsUsable);
            var degradedBatch = Assert.Single(result, b => b.BatchId == BatchOnNodeBId);
            AssertDegradedFromCache(degradedBatch, "Node is not ready");
            nodeBClientMock.Verify(c => c.GetOwnedPostageBatchesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory, MemberData(nameof(NodeFailureTests))]
        public async Task GetOwnedPostageBatchesReportsBatchesOfFailingNodeFromCache(
            Exception exception,
            string expectedError)
        {
            // Setup.
            nodeAClientMock.Setup(c => c.GetOwnedPostageBatchesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([NewLivePostageBatch(BatchOnNodeAId, isUsable: true)]);
            nodeBClientMock.Setup(c => c.GetOwnedPostageBatchesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
            postageBatchesCache.Add(NewPostageBatchCache(BatchOnNodeAId, NodeAId));
            postageBatchesCache.Add(NewPostageBatchCache(BatchOnNodeBId, NodeBId));

            // Action.
            var result = (await service.GetOwnedPostageBatchesAsync()).ToArray();

            // Assert.
            Assert.Equal(2, result.Length);
            Assert.Null(Assert.Single(result, b => b.BatchId == BatchOnNodeAId).Error);
            AssertDegradedFromCache(Assert.Single(result, b => b.BatchId == BatchOnNodeBId), expectedError);
        }

        [Fact]
        public async Task TryGetPostageBatchDetailsDoesNotAskNotAliveOwnerNode()
        {
            // Setup.
            nodeB.Status.FailedHeartbeatAttempt(["Node is not ready"], DateTime.UtcNow);
            postageBatchesCache.Add(NewPostageBatchCache(BatchOnNodeBId, NodeBId));

            // Action.
            var result = await service.TryGetPostageBatchDetailsAsync(BatchOnNodeBId);

            // Assert.
            Assert.NotNull(result);
            AssertDegradedFromCache(result, "Node is not ready");
            nodeBClientMock.Verify(c => c.GetPostageBatchAsync(It.IsAny<PostageBatchId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task TryGetPostageBatchDetailsReturnsNullOnNotFoundAnswer()
        {
            // Setup.
            nodeBClientMock.Setup(c => c.GetPostageBatchAsync(BatchOnNodeBId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(NewApiException(404, """{"code":404,"message":"issuer does not exist"}"""));
            postageBatchesCache.Add(NewPostageBatchCache(BatchOnNodeBId, NodeBId));

            // Action.
            var result = await service.TryGetPostageBatchDetailsAsync(BatchOnNodeBId);

            // Assert.
            Assert.Null(result);
        }

        [Theory, MemberData(nameof(NodeFailureTests))]
        public async Task TryGetPostageBatchDetailsReportsFailingOwnerNodeFromCache(
            Exception exception,
            string expectedError)
        {
            // Setup.
            nodeBClientMock.Setup(c => c.GetPostageBatchAsync(BatchOnNodeBId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);
            postageBatchesCache.Add(NewPostageBatchCache(BatchOnNodeBId, NodeBId));

            // Action.
            var result = await service.TryGetPostageBatchDetailsAsync(BatchOnNodeBId);

            // Assert.
            Assert.NotNull(result);
            AssertDegradedFromCache(result, expectedError);
        }

        [Fact]
        public async Task TryGetPostageBatchDetailsReturnsLiveDetails()
        {
            // Setup.
            nodeBClientMock.Setup(c => c.GetPostageBatchAsync(BatchOnNodeBId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NewLivePostageBatch(BatchOnNodeBId, isUsable: true));
            postageBatchesCache.Add(NewPostageBatchCache(BatchOnNodeBId, NodeBId));

            // Action.
            var result = await service.TryGetPostageBatchDetailsAsync(BatchOnNodeBId);

            // Assert.
            Assert.NotNull(result);
            Assert.Equal(BzzValue.FromPlurDecimal(1_000_000), result.Amount);
            Assert.Equal(BatchOnNodeBId, result.BatchId);
            Assert.Equal(42ul, result.BlockNumber);
            Assert.Equal(22, result.Depth);
            Assert.Null(result.Error);
            Assert.True(result.Exists);
            Assert.False(result.IsImmutable);
            Assert.True(result.IsUsable);
            Assert.Equal("live", result.Label);
            Assert.Equal(TimeSpan.FromDays(1), result.Ttl);
            Assert.Equal(3u, result.Utilization);
        }

        [Fact]
        public async Task TryGetPostageBatchDetailsReturnsNullWithUnknownBatch()
        {
            // Action.
            var result = await service.TryGetPostageBatchDetailsAsync(BatchOnNodeBId);

            // Assert.
            Assert.Null(result);
        }

        [Fact]
        public async Task VerifyPostageBatchUsableAcceptsUsableBatch()
        {
            // Setup.
            var postageBatchCache = NewPostageBatchCache(BatchOnNodeBId, NodeBId);
            nodeBClientMock.Setup(c => c.GetPostageBatchAsync(BatchOnNodeBId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NewLivePostageBatch(BatchOnNodeBId, isUsable: true));

            // Action.
            await service.VerifyPostageBatchUsableAsync(postageBatchCache);

            // Assert.
            nodeBClientMock.Verify(c => c.GetPostageBatchAsync(BatchOnNodeBId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task VerifyPostageBatchUsableRefusesBatchNotFoundOnOwnerNode()
        {
            // Setup.
            var postageBatchCache = NewPostageBatchCache(BatchOnNodeBId, NodeBId);
            nodeBClientMock.Setup(c => c.GetPostageBatchAsync(BatchOnNodeBId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(NewApiException(404, """{"code":404,"message":"issuer does not exist"}"""));

            // Action & assert.
            await Assert.ThrowsAsync<PostageBatchNotFoundException>(
                () => service.VerifyPostageBatchUsableAsync(postageBatchCache));
        }

        [Fact]
        public async Task VerifyPostageBatchUsableRefusesNotAliveOwnerNodeWithItsReason()
        {
            // Setup.
            var postageBatchCache = NewPostageBatchCache(BatchOnNodeBId, NodeBId);
            nodeB.Status.FailedHeartbeatAttempt(["Node is not ready"], DateTime.UtcNow);

            // Action & assert.
            var exception = await Assert.ThrowsAsync<BeeNodeUnavailableException>(
                () => service.VerifyPostageBatchUsableAsync(postageBatchCache));
            Assert.Equal("Node is not ready", exception.Message);
            nodeBClientMock.Verify(c => c.GetPostageBatchAsync(It.IsAny<PostageBatchId>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task VerifyPostageBatchUsableRefusesNotUsableBatch()
        {
            // Setup.
            var postageBatchCache = NewPostageBatchCache(BatchOnNodeBId, NodeBId);
            nodeBClientMock.Setup(c => c.GetPostageBatchAsync(BatchOnNodeBId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(NewLivePostageBatch(BatchOnNodeBId, isUsable: false));

            // Action & assert.
            await Assert.ThrowsAsync<PostageBatchNotUsableException>(
                () => service.VerifyPostageBatchUsableAsync(postageBatchCache));
        }

        [Theory, MemberData(nameof(NodeFailureTests))]
        public async Task VerifyPostageBatchUsableReportsFailingOwnerNodeReason(
            Exception exception,
            string expectedError)
        {
            // Setup.
            var postageBatchCache = NewPostageBatchCache(BatchOnNodeBId, NodeBId);
            nodeBClientMock.Setup(c => c.GetPostageBatchAsync(BatchOnNodeBId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Action & assert.
            var result = await Assert.ThrowsAsync<BeeNodeUnavailableException>(
                () => service.VerifyPostageBatchUsableAsync(postageBatchCache));
            Assert.Equal(expectedError, result.Message);
        }

        // Helpers.
        private static void AssertDegradedFromCache(PostageBatchResult details, string expectedError)
        {
            Assert.Null(details.Amount);
            Assert.Null(details.BlockNumber);
            Assert.Equal(20, details.Depth);
            Assert.Equal(expectedError, details.Error);
            Assert.Null(details.Exists);
            Assert.True(details.IsImmutable);
            Assert.False(details.IsUsable);
            Assert.Equal("", details.Label);
            Assert.Null(details.Ttl);
            Assert.Equal(7u, details.Utilization);
        }

        private static SwarmSdkApiException NewApiException(int statusCode, string responseBody) =>
            new(
                $"The HTTP status code of the response was not expected ({statusCode}).",
                statusCode,
                responseBody,
                new Dictionary<string, IEnumerable<string>>(),
                null);

        private static PostageBatch NewLivePostageBatch(PostageBatchId batchId, bool isUsable) =>
            new(
                id: batchId,
                amount: BzzValue.FromPlurDecimal(1_000_000),
                blockNumber: 42,
                depth: 22,
                exists: true,
                isImmutable: false,
                isUsable: isUsable,
                label: "live",
                ttl: TimeSpan.FromDays(1),
                utilization: 3);

        private static PostageBatchCache NewPostageBatchCache(PostageBatchId batchId, string ownerNodeId)
        {
            var buckets = new uint[PostageBuckets.BucketsSize];
            buckets[5] = 7; //the fullest bucket gives the utilization
            buckets[6] = 2;
            return new PostageBatchCache(batchId, buckets, 20, true, ownerNodeId);
        }
    }
}

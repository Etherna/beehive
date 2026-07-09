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

using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Utilities.DevNode
{
    public class DevNodeClientTest
    {
        // Fields.
        private readonly DevNodeClient devNodeClient = new();

        // Tests.

        [Fact]
        public async Task BuyPostageBatchCreatesReadableBatch()
        {
            // Action.
            var (batchId, txHash) = await devNodeClient.BuyPostageBatchAsync(
                BzzValue.FromPlurLong(10_000_000),
                20,
                label: "test",
                immutable: false);

            // Assert.
            Assert.Equal(EthTxHash.Zero, txHash);

            var batch = await devNodeClient.GetPostageBatchAsync(batchId);
            Assert.Equal(batchId, batch.Id);
            Assert.Equal(BzzValue.FromPlurLong(10_000_000), batch.Amount);
            Assert.Equal(20, batch.Depth);
            Assert.Equal("test", batch.Label);
            Assert.False(batch.IsImmutable);
            Assert.True(batch.IsUsable);
            Assert.True(batch.Exists);
        }

        [Fact]
        public async Task BuyPostageBatchGeneratesUniqueBatchIds()
        {
            // Action.
            var (batchId0, _) = await devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), 20);
            var (batchId1, _) = await devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), 20);

            // Assert.
            Assert.NotEqual(batchId0, batchId1);
        }

        [Theory]
        [InlineData(PostageBatch.MinDepth - 1)]
        [InlineData(PostageBatch.MaxDepth + 1)]
        public async Task BuyPostageBatchThrowsWithInvalidDepth(int depth)
        {
            // Action & assert.
            var exception = await Assert.ThrowsAsync<SwarmSdkApiException>(
                () => devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), depth));
            Assert.Equal(400, exception.StatusCode);
        }

        [Fact]
        public async Task DilutePostageBatchIncreasesDepth()
        {
            // Setup.
            var (batchId, _) = await devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), 20);

            // Action.
            await devNodeClient.DilutePostageBatchAsync(batchId, 22);

            // Assert.
            var batch = await devNodeClient.GetPostageBatchAsync(batchId);
            Assert.Equal(22, batch.Depth);
        }

        [Fact]
        public async Task DilutePostageBatchThrowsWithImmutableBatch()
        {
            // Setup.
            var (batchId, _) = await devNodeClient.BuyPostageBatchAsync(
                BzzValue.FromPlurLong(1_000_000),
                20,
                immutable: true);

            // Action & assert.
            var exception = await Assert.ThrowsAsync<SwarmSdkApiException>(
                () => devNodeClient.DilutePostageBatchAsync(batchId, 22));
            Assert.Equal(400, exception.StatusCode);
        }

        [Fact]
        public async Task DilutePostageBatchThrowsWithNotGreaterDepth()
        {
            // Setup.
            var (batchId, _) = await devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), 20);

            // Action & assert.
            var exception = await Assert.ThrowsAsync<SwarmSdkApiException>(
                () => devNodeClient.DilutePostageBatchAsync(batchId, 20));
            Assert.Equal(400, exception.StatusCode);
        }

        [Fact]
        public async Task DilutePostageBatchThrowsWithUnknownBatch()
        {
            // Action & assert.
            var exception = await Assert.ThrowsAsync<SwarmSdkApiException>(
                () => devNodeClient.DilutePostageBatchAsync(PostageBatchId.Zero, 22));
            Assert.Equal(404, exception.StatusCode);
        }

        [Fact]
        public async Task GetGlobalValidPostageBatchesReturnsBoughtBatches()
        {
            // Setup.
            var (batchId0, _) = await devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), 20);
            var (batchId1, _) = await devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), 20);

            // Action.
            var allBatches = await devNodeClient.GetGlobalValidPostageBatchesAsync();
            var filteredBatches = await devNodeClient.GetGlobalValidPostageBatchesAsync(batchId1);

            // Assert.
            Assert.Equal(2, allBatches.Length);
            Assert.Contains(allBatches, pair => pair.PostageBatch.Id == batchId0);
            var (batch, owner) = Assert.Single(filteredBatches);
            Assert.Equal(batchId1, batch.Id);
            Assert.Equal(DevNodeClient.EthereumAddress, owner);
        }

        [Fact]
        public async Task GetHealthReportsHealthyAndReadyNode()
        {
            // Action.
            var health = await devNodeClient.GetHealthAsync();
            var isReady = await devNodeClient.GetReadinessAsync();

            // Assert.
            Assert.True(health.IsStatusOk);
            Assert.True(isReady);
        }

        [Fact]
        public async Task GetOwnedPostageBatchesReturnsBoughtBatches()
        {
            // Setup.
            var (batchId, _) = await devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), 20);

            // Action.
            var batches = await devNodeClient.GetOwnedPostageBatchesAsync();

            // Assert.
            var batch = Assert.Single(batches);
            Assert.Equal(batchId, batch.Id);
        }

        [Fact]
        public async Task GetPostageBatchBucketsReturnsEmptyBuckets()
        {
            // Setup.
            var (batchId, _) = await devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), 20);

            // Action.
            var buckets = await devNodeClient.GetPostageBatchBucketsAsync(batchId);

            // Assert.
            Assert.Equal(PostageBatch.BucketDepth, buckets.BucketDepth);
            Assert.Equal(20, buckets.Depth);
            Assert.Equal(1u << (20 - PostageBatch.BucketDepth), buckets.BucketUpperBound);
            Assert.Equal(1 << PostageBatch.BucketDepth, buckets.Collisions.Count());
            Assert.All(buckets.Collisions, collisions => Assert.Equal(0u, collisions));
        }

        [Fact]
        public async Task GetPostageBatchThrowsWithUnknownBatch()
        {
            // Action & assert.
            var exception = await Assert.ThrowsAsync<SwarmSdkApiException>(
                () => devNodeClient.GetPostageBatchAsync(PostageBatchId.Zero));
            Assert.Equal(404, exception.StatusCode);
        }

        [Fact]
        public async Task TopUpPostageBatchIncreasesAmount()
        {
            // Setup.
            var (batchId, _) = await devNodeClient.BuyPostageBatchAsync(BzzValue.FromPlurLong(1_000_000), 20);

            // Action.
            await devNodeClient.TopUpPostageBatchAsync(batchId, BzzValue.FromPlurLong(500_000));

            // Assert.
            var batch = await devNodeClient.GetPostageBatchAsync(batchId);
            Assert.Equal(BzzValue.FromPlurLong(1_500_000), batch.Amount);
        }

        [Fact]
        public async Task TopUpPostageBatchThrowsWithUnknownBatch()
        {
            // Action & assert.
            var exception = await Assert.ThrowsAsync<SwarmSdkApiException>(
                () => devNodeClient.TopUpPostageBatchAsync(PostageBatchId.Zero, BzzValue.FromPlurLong(500_000)));
            Assert.Equal(404, exception.StatusCode);
        }
    }
}

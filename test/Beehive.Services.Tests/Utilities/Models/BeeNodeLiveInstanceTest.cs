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

using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Helpers;
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Utilities.Models
{
    public class BeeNodeLiveInstanceTest
    {
        // Consts.
        private static readonly PostageBatchId BatchId = "aa00000000000000000000000000000000000000000000000000000000000001";
        private const string NodeId = "nodeA";
        private static readonly TimeSpan StatusCallTimeout = TimeSpan.FromMilliseconds(200);

        // Fields.
        private readonly Mock<ISwarmClient> clientMock = new();
        private readonly BeeNodeLiveInstance instance;
        private bool isHealthy = true;
        private readonly CapturingLogger logger = new();

        // Constructor.
        public BeeNodeLiveInstanceTest()
        {
            // The node answers its health from the switch of the test, the other heartbeat calls succeed.
            clientMock.Setup(c => c.GetHealthAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new Health(isHealthy, "2.8.1", "7.3.0"));
            clientMock.Setup(c => c.GetReadinessAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            clientMock.Setup(c => c.GetNodeAddressesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SwarmNodeAddresses(
                    "0000000000000000000000000000000000000000000000000000000000000001",
                    [],
                    "0x0000000000000000000000000000000000000001",
                    "0x0000000000000000000000000000000000000001",
                    "020000000000000000000000000000000000000000000000000000000000000001",
                    "030000000000000000000000000000000000000000000000000000000000000001"));

            instance = new BeeNodeLiveInstance(NodeId, clientMock.Object, true, logger, StatusCallTimeout);
        }

        // Tests.

        [Fact]
        public async Task GetPostageBatchFailsWithTheTimeoutReasonOnANodeNotAnswering()
        {
            // Setup.
            //the node accepts the call and never answers: only its token ends it
            clientMock.Setup(c => c.GetPostageBatchAsync(BatchId, It.IsAny<CancellationToken>()))
                .Returns(async (PostageBatchId _, CancellationToken cancellationToken) =>
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    throw new InvalidOperationException();
                });

            // Action.
            var read = instance.GetPostageBatchAsync(BatchId);
            var ended = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(5)));

            // Assert.
            Assert.Same(read, ended); //ended by the timeout, not by the node
            var exception = await Assert.ThrowsAsync<TimeoutException>(() => read);
            Assert.Equal("Node did not answer within 0.2s", exception.Message);
            Assert.True(exception.IsBeeNodeCallFailure()); //reported as the node's reason by the callers
        }

        [Fact]
        public async Task LogsTheFirstHeartbeatAndTheAliveTransitionsOnly()
        {
            // Action.
            await instance.TryRefreshStatusAsync(); //the first heartbeat, alive
            await instance.TryRefreshStatusAsync(); //still alive
            isHealthy = false;
            await instance.TryRefreshStatusAsync(); //not alive anymore
            await instance.TryRefreshStatusAsync(); //still not alive
            isHealthy = true;
            await instance.TryRefreshStatusAsync(); //alive again

            // Assert.
            Assert.True(instance.Status.IsAlive);
            (LogLevel Level, string EventName)[] expectedEvents =
            [
                (LogLevel.Information, "BeeNodeAlive"),
                (LogLevel.Warning, "BeeNodeNotAlive"),
                (LogLevel.Information, "BeeNodeAlive")
            ];
            Assert.Equal(expectedEvents, logger.Events.Select(e => (e.Level, e.EventName)));
            Assert.Contains("Node is not healthy", logger.Events[1].Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task LogsTheFirstHeartbeatOfANodeNotAnswering()
        {
            // Setup.
            clientMock.Setup(c => c.GetHealthAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("Connection refused"));

            // Action.
            await instance.TryRefreshStatusAsync();
            await instance.TryRefreshStatusAsync();

            // Assert.
            Assert.False(instance.Status.IsAlive);
            var logged = Assert.Single(logger.Events);
            Assert.Equal(LogLevel.Warning, logged.Level);
            Assert.Equal("BeeNodeNotAlive", logged.EventName);
            Assert.Contains("Exception invoking node API", logged.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReportsNotAliveANodeNotAnsweringWithinTheTimeout()
        {
            // Setup.
            //the node accepts the call and never answers: only its token ends it
            clientMock.Setup(c => c.GetHealthAsync(It.IsAny<CancellationToken>()))
                .Returns(async (CancellationToken cancellationToken) =>
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                    return new Health(true, "2.8.1", "7.3.0");
                });

            // Action.
            var refresh = instance.TryRefreshStatusAsync();
            var ended = await Task.WhenAny(refresh, Task.Delay(TimeSpan.FromSeconds(5)));

            // Assert.
            Assert.Same(refresh, ended); //ended by the timeout, not by the node
            Assert.False(await refresh);
            Assert.Equal("Node did not answer within 0.2s", instance.Status.NotAliveReason);
            var logged = Assert.Single(logger.Events);
            Assert.Equal(LogLevel.Warning, logged.Level);
            Assert.Equal("BeeNodeNotAlive", logged.EventName);
            Assert.Contains("Node did not answer within 0.2s", logged.Message, StringComparison.Ordinal);
        }
    }
}

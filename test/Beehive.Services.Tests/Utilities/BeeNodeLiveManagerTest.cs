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
using Etherna.Beehive.Services.Options;
using Etherna.Scrinium.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Utilities
{
    public sealed class BeeNodeLiveManagerTest : IDisposable
    {
        // Fields.
        private readonly ConcurrentQueue<Socket> acceptedSockets = [];
        private readonly TcpListener hungNode = new(IPAddress.Loopback, 0); //accepts the connections and never answers
        private readonly BeeNodeLiveManager manager;

        // Constructor.
        public BeeNodeLiveManagerTest()
        {
            hungNode.Start();
            _ = AcceptForeverAsync();

            manager = new BeeNodeLiveManager(
                new OptionsWrapper<DevNodeOptions>(new DevNodeOptions()),
                NullLogger<BeeNodeLiveManager>.Instance,
                new Mock<IServiceScopeFactory>().Object);
        }

        // Dispose.
        public void Dispose()
        {
            hungNode.Dispose();
            foreach (var socket in acceptedSockets)
                socket.Dispose();
            manager.Dispose();
        }

        // Tests.
        [Fact]
        public async Task HeartbeatRoundLogsAnyExceptionAndKeepsTheTimerAlive()
        {
            // Setup: the db sync throws what its own catch doesn't expect, as a Mongo timeout does.
            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock.Setup(f => f.CreateScope())
                .Throws(new InvalidOperationException("Waiting for suitable server to become available"));
            var logger = new CapturingLogger<BeeNodeLiveManager>();
            using var failingManager = new BeeNodeLiveManager(
                new OptionsWrapper<DevNodeOptions>(new DevNodeOptions()),
                logger,
                scopeFactoryMock.Object);

            // Action: a round run directly, then the rounds of the timer. An exception escaping the async void
            // callback of the timer would terminate the test host.
            var round = Record.ExceptionAsync(failingManager.HeartbeatCallbackAsync);
            failingManager.StartHealthHeartbeat();
            for (var i = 0; i < 30 && logger.Events.ToArray().Count(e => e.EventName == "BeeNodesHeartbeatFailed") < 2; i++)
                await Task.Delay(100);
            failingManager.StopHealthHeartbeat();

            // Assert.
            Assert.Null(await round);
            var failures = logger.Events.ToArray().Where(e => e.EventName == "BeeNodesHeartbeatFailed").ToArray();
            Assert.Equal(2, failures.Length); //the direct round and the first tick of the timer
            Assert.All(failures, e => Assert.Equal(LogLevel.Error, e.Level));
        }

        [Fact]
        public async Task TryAddBeeNodeRegistersTheNodeWithoutCallingIt()
        {
            // Setup.
            var beeNode = new BeeNode(new Uri($"http://127.0.0.1:{((IPEndPoint)hungNode.LocalEndpoint).Port}/"), true);
            ReflectionHelper.SetValue(beeNode, n => n.Id, "nodeA");

            // Action.
            var add = Task.Run(() => manager.TryAddBeeNode(beeNode));
            var ended = await Task.WhenAny(add, Task.Delay(TimeSpan.FromSeconds(3)));

            // Assert.
            Assert.Same(add, ended); //the node, never answering, held nothing
            var instance = await add;
            Assert.Same(instance, Assert.Single(manager.AllNodes));
            Assert.False(instance.Status.IsAlive);
            Assert.Equal(default, instance.Status.HeartbeatTimeStamp); //its first heartbeat belongs to the heartbeat round
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            Assert.Empty(acceptedSockets); //no connection was ever opened to the node
        }

        // Helpers.
        private async Task AcceptForeverAsync()
        {
            try
            {
                while (true)
                    acceptedSockets.Enqueue(await hungNode.AcceptSocketAsync());
            }
            catch (ObjectDisposedException) { } //stopped
            catch (SocketException) { } //stopped
        }
    }
}

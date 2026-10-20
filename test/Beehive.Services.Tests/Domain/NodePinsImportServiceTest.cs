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
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Models;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
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
    public class NodePinsImportServiceTest
    {
        // Consts.
        private const string JobId = "job-1";
        private const string NodeId = "nodeA";

        private static readonly SwarmReference ReferenceIncomplete = "aa00000000000000000000000000000000000000000000000000000000000002";
        private static readonly SwarmReference ReferenceNew = "aa00000000000000000000000000000000000000000000000000000000000001";
        private static readonly SwarmReference ReferencePinned = "aa00000000000000000000000000000000000000000000000000000000000003";

        // Fields.
        private readonly Mock<IBackgroundJobClient> backgroundJobClientMock = new();
        private readonly List<ChunkPin> chunkPins = [];
        private readonly Mock<IHangfireJobsMonitor> hangfireJobsMonitorMock = new();
        private readonly List<NodePinsImport> imports = [];
        private readonly Mock<ISwarmClient> nodeClientMock = new();
        private readonly NodePinsImportService service;

        // Constructor.
        public NodePinsImportServiceTest()
        {
            // The imports in memory: queries run over them, creations get an id.
            var importsMock = new Mock<IRepository<NodePinsImport, string>>();
            importsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<NodePinsImport>, Task<List<NodePinsImport>>>>(),
                    null))
                .Returns((Func<IQueryable<NodePinsImport>, Task<List<NodePinsImport>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(imports)));
            importsMock.Setup(r => r.DeleteAsync(
                    It.IsAny<NodePinsImport>(),
                    It.IsAny<FilterDefinition<NodePinsImport>[]?>(),
                    It.IsAny<CancellationToken>()))
                .Callback((NodePinsImport import, FilterDefinition<NodePinsImport>[]? _, CancellationToken _) =>
                    imports.Remove(import))
                .Returns(Task.CompletedTask);
            importsMock.Setup(r => r.CreateAsync(It.IsAny<NodePinsImport>(), It.IsAny<CancellationToken>()))
                .Callback((NodePinsImport import, CancellationToken _) =>
                {
                    ReflectionHelper.SetValue(import, i => i.Id, Guid.NewGuid().ToString());
                    imports.Add(import);
                })
                .Returns(Task.CompletedTask);
            var chunkPinsMock = new Mock<IRepository<ChunkPin, string>>();
            chunkPinsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<ChunkPin>, Task<List<SwarmReference>>>>(),
                    null))
                .Returns((Func<IQueryable<ChunkPin>, Task<List<SwarmReference>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(chunkPins)));
            var dbContextMock = new Mock<IBeehiveDbContext>();
            dbContextMock.Setup(c => c.ChunkPins).Returns(chunkPinsMock.Object);
            dbContextMock.Setup(c => c.NodePinsImports).Returns(importsMock.Object);

            // A registered node, alive.
            var node = new BeeNodeLiveInstance(NodeId, nodeClientMock.Object, true, NullLogger.Instance);
            node.Status.SucceededHeartbeatAttempt(DateTime.UtcNow);
            var beeNodeLiveManagerMock = new Mock<IBeeNodeLiveManager>();
            beeNodeLiveManagerMock.Setup(m => m.GetBeeNodeLiveInstanceAsync(NodeId)).ReturnsAsync(node);

            backgroundJobClientMock.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns(JobId);
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(true);

            service = new NodePinsImportService(
                backgroundJobClientMock.Object,
                dbContextMock.Object,
                beeNodeLiveManagerMock.Object,
                hangfireJobsMonitorMock.Object,
                NullLogger<NodePinsImportService>.Instance);
        }

        // Tests.

        [Theory]
        [InlineData(false, 1, "failed, running")]
        [InlineData(true, 1, "completed, running")]
        [InlineData(null, 2, "running")]
        public async Task DeleteImportsDeletesTheFinishedImportsOfTheAskedOutcomes(bool? failed, int expectedDeleted, string expectedLeft)
        {
            // Setup.
            var completedImport = await service.StartImportAsync(NodeId, unpinImported: false);
            completedImport.Start();
            completedImport.Complete();
            var failedImport = await service.StartImportAsync(NodeId, unpinImported: false);
            failedImport.Start();
            failedImport.Fail("the node went away");
            var runningImport = await service.StartImportAsync(NodeId, unpinImported: false);
            runningImport.Start();
            var otherNodeImport = await service.StartImportAsync("nodeB", unpinImported: false);
            otherNodeImport.Start();
            otherNodeImport.Complete();
            var names = new Dictionary<NodePinsImport, string>
            {
                [completedImport] = "completed", [failedImport] = "failed", [runningImport] = "running"
            };

            // Action.
            var deletedImports = await service.DeleteImportsAsync(NodeId, failed);

            // Assert.
            Assert.Equal(expectedDeleted, deletedImports);
            Assert.Equal(expectedLeft, string.Join(", ", imports.Where(i => i != otherNodeImport).Select(i => names[i])));
            Assert.Contains(otherNodeImport, imports);
        }

        [Fact]
        public async Task GetImportsListsEveryImportNewestFirstAndFailsTheOrphanOnes()
        {
            // Setup.
            var oldImport = await service.StartImportAsync(NodeId, unpinImported: false);
            ReflectionHelper.SetValue(oldImport, i => i.CreationDateTime, new DateTime(2026, 10, 18, 10, 00, 00));
            oldImport.Start();
            oldImport.Complete();
            var otherNodeImport = await service.StartImportAsync("nodeB", unpinImported: false);
            ReflectionHelper.SetValue(otherNodeImport, i => i.CreationDateTime, new DateTime(2026, 10, 18, 11, 00, 00));
            otherNodeImport.Start();
            var newImport = await service.StartImportAsync(NodeId, unpinImported: true);
            ReflectionHelper.SetValue(newImport, i => i.CreationDateTime, new DateTime(2026, 10, 18, 12, 00, 00));
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(false); //every job gone

            // Action.
            var imports = await service.GetImportsAsync(0, 10);

            // Assert.
            Assert.Equal([newImport, otherNodeImport, oldImport], imports);
            Assert.Equal(NodePinsImportState.Failed, newImport.State);
            Assert.Equal(NodePinsImportState.Failed, otherNodeImport.State);
            Assert.Equal(NodePinsImportState.Completed, oldImport.State);
        }

        [Fact]
        public async Task GetPinsSnapshotTellsTheContentsBeehiveDoesNotHoldInFull()
        {
            // Setup.
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceNew, ReferencePinned, ReferenceIncomplete]);
            var pinnedPin = new ChunkPin(ReferencePinned);
            pinnedPin.UpdateProcessed([], 3);
            var incompletePin = new ChunkPin(ReferenceIncomplete);
            incompletePin.UpdateProcessed(["bb00000000000000000000000000000000000000000000000000000000000009"], 2);
            chunkPins.AddRange([pinnedPin, incompletePin]);

            // Action.
            var snapshot = await service.GetPinsSnapshotAsync(NodeId);

            // Assert.
            Assert.Equal([ReferenceNew, ReferencePinned, ReferenceIncomplete], snapshot.PinnedOnNode);
            Assert.Equal([ReferenceNew, ReferenceIncomplete], snapshot.NotImported);
        }

        [Fact]
        public async Task StartImportRecordsTheImportAndEnqueuesItsJob()
        {
            // Action.
            var import = await service.StartImportAsync(NodeId, unpinImported: true);

            // Assert.
            Assert.Equal(NodeId, import.NodeId);
            Assert.True(import.UnpinImported);
            Assert.True(import.ImportsEveryPin);
            Assert.Equal(JobId, import.JobId);
            Assert.Equal(NodePinsImportState.Pending, import.State);
            Assert.Single(imports);
            backgroundJobClientMock.Verify(c => c.Create(
                It.Is<Job>(j => j.Type == typeof(Tasks.Trigger.IImportNodePinsTask) && j.Args.Contains(import.Id)),
                It.IsAny<EnqueuedState>()), Times.Once);
        }

        [Fact]
        public async Task StartImportRecordsTheRequestedReferences()
        {
            // Action.
            var import = await service.StartImportAsync(NodeId, unpinImported: false, [ReferenceNew, ReferenceIncomplete]);

            // Assert.
            Assert.False(import.ImportsEveryPin);
            Assert.Equal([ReferenceNew, ReferenceIncomplete], import.RequestedReferences);
        }

        [Fact]
        public async Task StartImportRefusesANodeWithARunningImport()
        {
            // Setup.
            await service.StartImportAsync(NodeId, unpinImported: false);

            // Action & assert.
            await Assert.ThrowsAsync<ResourceLockException>(
                () => service.StartImportAsync(NodeId, unpinImported: false));
            Assert.Single(imports);
        }

        [Fact]
        public async Task StartImportAcceptsANodeWhoseLastImportCompleted()
        {
            // Setup.
            var firstImport = await service.StartImportAsync(NodeId, unpinImported: false);
            firstImport.Start();
            firstImport.Complete();

            // Action.
            var secondImport = await service.StartImportAsync(NodeId, unpinImported: false);

            // Assert.
            Assert.NotSame(firstImport, secondImport);
            Assert.Equal(2, imports.Count);
        }

        [Fact]
        public async Task TryGetLatestImportFailsAnImportWhoseJobIsGone()
        {
            // Setup.
            var import = await service.StartImportAsync(NodeId, unpinImported: false);
            import.Start();
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(false);

            // Action.
            var latestImport = await service.TryGetLatestImportAsync(NodeId);

            // Assert.
            Assert.Same(import, latestImport);
            Assert.Equal(NodePinsImportState.Failed, import.State);
            Assert.NotNull(import.FailureReason);
        }

        [Fact]
        public async Task TryGetLatestImportReturnsNullForANeverImportedNode()
        {
            // Action.
            var latestImport = await service.TryGetLatestImportAsync(NodeId);

            // Assert.
            Assert.Null(latestImport);
        }
    }
}

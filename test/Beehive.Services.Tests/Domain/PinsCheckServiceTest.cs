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
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Helpers;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Utility;
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
    public class PinsCheckServiceTest
    {
        // Consts.
        private const string JobId = "job-1";
        private static readonly SwarmReference Reference = "aa00000000000000000000000000000000000000000000000000000000000001";

        // Fields.
        private readonly Mock<IBackgroundJobClient> backgroundJobClientMock = new();
        private readonly List<PinsCheck> checks = [];
        private readonly Mock<IHangfireJobsMonitor> hangfireJobsMonitorMock = new();
        private readonly Mock<IPinIntegrityService> pinIntegrityServiceMock = new();
        private readonly List<FakeResourceLockLease> pinsCheckLeases = [];
        private readonly PinsCheckService service;

        // Constructor.
        public PinsCheckServiceTest()
        {
            // The checks in memory: queries run over them, creations get an id.
            var checksMock = new Mock<IRepository<PinsCheck, string>>();
            checksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<PinsCheck>, Task<List<PinsCheck>>>>(),
                    null))
                .Returns((Func<IQueryable<PinsCheck>, Task<List<PinsCheck>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(checks)));
            checksMock.Setup(r => r.FindOneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => checks.Single(c => c.Id == id));
            checksMock.Setup(r => r.CreateAsync(It.IsAny<PinsCheck>(), It.IsAny<CancellationToken>()))
                .Callback((PinsCheck check, CancellationToken _) =>
                {
                    ReflectionHelper.SetValue(check, c => c.Id, Guid.NewGuid().ToString());
                    ReflectionHelper.SetValue(check, c => c.CreationDateTime, DateTime.UtcNow.AddTicks(checks.Count));
                    checks.Add(check);
                })
                .Returns(Task.CompletedTask);
            checksMock.Setup(r => r.DeleteAsync(
                    It.IsAny<PinsCheck>(),
                    It.IsAny<FilterDefinition<PinsCheck>[]?>(),
                    It.IsAny<CancellationToken>()))
                .Callback((PinsCheck check, FilterDefinition<PinsCheck>[]? _, CancellationToken _) =>
                    checks.Remove(check))
                .Returns(Task.CompletedTask);
            var dbContextMock = new Mock<IBeehiveDbContext>();
            dbContextMock.Setup(c => c.PinsChecks).Returns(checksMock.Object);

            backgroundJobClientMock.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns(JobId);
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(true);

            // The lock of the pins check is free, unless a test has a check of every pin hold it.
            pinIntegrityServiceMock.Setup(s => s.TryAcquirePinsCheckLockAsync())
                .ReturnsAsync(() =>
                {
                    var lease = new FakeResourceLockLease();
                    pinsCheckLeases.Add(lease);
                    return lease;
                });

            service = new PinsCheckService(
                backgroundJobClientMock.Object,
                dbContextMock.Object,
                hangfireJobsMonitorMock.Object,
                NullLogger<PinsCheckService>.Instance,
                pinIntegrityServiceMock.Object);
        }

        // Tests.

        [Fact]
        public async Task AnswersAnEndedCheckAsItIsOnAStop()
        {
            // Setup.
            var check = await service.StartCheckAsync();
            check.Start(1, []);
            check.Complete();

            // Action.
            var result = await service.StopCheckAsync(check.Id);

            // Assert.
            Assert.Null(result.StopRequestedDateTime);
            Assert.Equal(PinsCheckState.Completed, result.State);
        }

        [Fact]
        public async Task AsksARunningCheckToStop()
        {
            // Setup.
            var check = await service.StartCheckAsync();
            check.Start(10, []);

            // Action.
            var result = await service.StopCheckAsync(check.Id);

            // Assert.
            Assert.NotNull(result.StopRequestedDateTime);
            Assert.Equal(PinsCheckState.Running, result.State); //its job stops it
        }

        [Theory]
        [InlineData(null, 3, "running")]
        [InlineData(true, 1, "completed,stopped,running")]
        [InlineData(false, 2, "failed,running")]
        public async Task DeletesTheFinishedChecksOfTheAskedOutcomes(bool? failed, int expectedDeleted, string expectedLeft)
        {
            // Setup: a completed, a failed, a stopped and a running check, each one started after the previous ended.
            Dictionary<PinsCheck, string> names = [];
            var completed = await service.StartCheckAsync();
            completed.Start(1, []);
            completed.Complete();
            names[completed] = "completed";
            var failedCheck = await service.StartCheckAsync();
            failedCheck.Fail("the db was away");
            names[failedCheck] = "failed";
            var stopped = await service.StartCheckAsync();
            stopped.Start(1, []);
            stopped.StopOnRequest();
            names[stopped] = "stopped";
            names[await service.StartCheckAsync()] = "running";

            // Action.
            var deletedChecks = await service.DeleteChecksAsync(failed);

            // Assert.
            Assert.Equal(expectedDeleted, deletedChecks);
            Assert.Equal(expectedLeft, string.Join(',', checks.Select(c => names[c])));
        }

        [Fact]
        public async Task FailsACheckWhoseJobIsGoneAndLetsANewOneStart()
        {
            // Setup: the server running the job stopped.
            var orphan = await service.StartCheckAsync();
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(false);

            // Action.
            var read = await service.GetCheckAsync(orphan.Id);

            // Assert.
            Assert.Equal(orphan, read);
            Assert.Equal(PinsCheckState.Failed, orphan.State);
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(true);
            await service.StartCheckAsync(); //not refused
            Assert.Equal(2, checks.Count);
        }

        [Fact]
        public async Task GetChecksAnswersThePageNewestFirstAndFailsTheRunningOneWhoseJobIsGone()
        {
            // Setup: a completed check and, started after it, one whose job is gone with the server running it.
            var completed = await service.StartCheckAsync();
            completed.Start(1, []);
            completed.Complete();
            var orphan = await service.StartCheckAsync();
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(false);

            // Action.
            var firstPage = await service.GetChecksAsync(0, 1);
            var bothPages = await service.GetChecksAsync(0, 10);

            // Assert.
            Assert.Equal([orphan], firstPage);
            Assert.Equal([orphan, completed], bothPages);
            Assert.Equal(PinsCheckState.Failed, orphan.State);
            Assert.Equal(PinsCheckState.Completed, completed.State); //an ended check is left as it ended
        }

        [Fact]
        public async Task RefusesAStateThatIsNoneOfAPin()
        {
            // Action and assert.
            await Assert.ThrowsAsync<ArgumentException>(() => service.StartCheckAsync([(ChunkPinState)7]));
            Assert.Empty(checks);
        }

        [Fact]
        public async Task RefusesAStateTogetherWithReferences()
        {
            // Action and assert.
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.StartCheckAsync([ChunkPinState.Succeeded], [Reference]));
            Assert.Empty(checks);
        }

        [Fact]
        public async Task RefusesToStartACheckWhileADeprecatedStoreHoldsChunks()
        {
            // Setup.
            pinIntegrityServiceMock.Setup(s => s.VerifyPinsCanBeCheckedAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DeprecatedChunksNotMigratedException("the deprecated chunk stores are not empty"));

            // Action and assert.
            await Assert.ThrowsAsync<DeprecatedChunksNotMigratedException>(() => service.StartCheckAsync());
            Assert.Empty(checks);
            backgroundJobClientMock.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
        }

        [Fact]
        public async Task RefusesToStartACheckWhileACheckOfEveryPinIsBeingAnswered()
        {
            // Setup.
            //the stream of GET /pins/check holds the lock of the pins check
            pinIntegrityServiceMock.Setup(s => s.TryAcquirePinsCheckLockAsync())
                .ReturnsAsync((IResourceLockLease?)null);

            // Action and assert.
            await Assert.ThrowsAsync<ResourceLockException>(() => service.StartCheckAsync());
            Assert.Empty(checks);
            backgroundJobClientMock.Verify(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Never);
        }

        [Fact]
        public async Task RefusesToStartACheckWhileAnotherOneRuns()
        {
            // Setup.
            await service.StartCheckAsync();

            // Action and assert.
            await Assert.ThrowsAsync<ResourceLockException>(() => service.StartCheckAsync());
            Assert.Single(checks);
            Assert.Single(pinsCheckLeases); //refused before asking for the lock
        }

        [Fact]
        public async Task StartsACheckAfterTheLastOneEnded()
        {
            // Setup.
            var stopped = await service.StartCheckAsync();
            stopped.Start(10, []);
            stopped.StopOnRequest();

            // Action.
            var check = await service.StartCheckAsync();

            // Assert.
            Assert.Equal(2, checks.Count);
            Assert.Equal(check, await service.TryGetLatestCheckAsync());
        }

        [Theory]
        [InlineData(new ChunkPinState[0])]
        [InlineData(new[] { ChunkPinState.Succeeded })]
        [InlineData(new[] { ChunkPinState.Incomplete, ChunkPinState.Unprocessed, ChunkPinState.Incomplete })]
        public async Task StartsACheckOfThePinsOfTheRequestedStatesAsABackgroundJob(ChunkPinState[] pinStates)
        {
            ArgumentNullException.ThrowIfNull(pinStates);

            // Action.
            var check = await service.StartCheckAsync(pinStates);

            // Assert.
            Assert.Equal(JobId, check.JobId);
            Assert.Equal(PinsCheckState.Pending, check.State);
            Assert.Equal(pinStates.Distinct(), check.RequestedPinStates); //a state asked twice is taken once
            Assert.False(check.ChecksRequestedReferences);
            Assert.Equal(check, Assert.Single(checks));
            backgroundJobClientMock.Verify(c => c.Create(
                    It.Is<Job>(j => j.Type == typeof(Tasks.Trigger.ICheckPinsTask) && (string)j.Args[0] == check.Id),
                    It.IsAny<IState>()),
                Times.Once);
            Assert.True(Assert.Single(pinsCheckLeases).IsReleased); //the lock is left to the job of the run
        }

        [Fact]
        public async Task StartsACheckOfTheRequestedReferences()
        {
            // Action.
            var check = await service.StartCheckAsync(references: [Reference, Reference]);

            // Assert.
            Assert.True(check.ChecksRequestedReferences);
            Assert.Equal([Reference], check.RequestedReferences);
            Assert.Empty(check.RequestedPinStates);
        }
    }
}

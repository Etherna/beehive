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
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Helpers;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Tasks.Trigger
{
    public class CheckPinsTaskTest
    {
        // Consts.
        private const string CheckId = "check-1";
        private static readonly SwarmHash InvalidChunkHash = "bb00000000000000000000000000000000000000000000000000000000000007";
        private static readonly SwarmHash MissingChunkHash = "bb00000000000000000000000000000000000000000000000000000000000009";
        private static readonly SwarmReference ReferenceComplete = "aa00000000000000000000000000000000000000000000000000000000000001";
        private static readonly SwarmReference ReferenceIncomplete = "aa00000000000000000000000000000000000000000000000000000000000002";
        private static readonly SwarmReference ReferenceInvalid = "aa00000000000000000000000000000000000000000000000000000000000003";

        // Fields.
        private PinsCheck check = NewCheck();
        private int checkLeaseDenials;
        private readonly List<FakeResourceLockLease> checkLeases = [];
        private readonly List<SwarmReference> checkedReferences = [];
        private readonly List<ChunkPin> chunkPins = [];
        private readonly Mock<IBeehiveDbContext> dbContextMock = new();
        private Action? onCheckLeaseDenied;
        private readonly Mock<IPinIntegrityService> pinIntegrityServiceMock = new();
        private readonly List<PinCheck> pinRecords = [];
        private readonly CheckPinsTask task;

        // Constructor.
        public CheckPinsTaskTest()
        {
            // The pins in memory: the queries of the task run as they are over them.
            var chunkPinsMock = new Mock<IRepository<ChunkPin, string>>();
            chunkPinsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<ChunkPin>, Task<List<ChunkPin>>>>(),
                    null))
                .Returns((Func<IQueryable<ChunkPin>, Task<List<ChunkPin>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(chunkPins)));
            chunkPinsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<ChunkPin>, Task<List<SwarmReference>>>>(),
                    null))
                .Returns((Func<IQueryable<ChunkPin>, Task<List<SwarmReference>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(chunkPins)));

            // The check under run, the reads of its stop request, and its pin records.
            var pinsChecksMock = new Mock<IRepository<PinsCheck, string>>();
            pinsChecksMock.Setup(r => r.FindOneAsync(CheckId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => check);
            pinsChecksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<PinsCheck>, Task<List<DateTime?>>>>(),
                    null))
                .Returns((Func<IQueryable<PinsCheck>, Task<List<DateTime?>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(new List<PinsCheck> { check })));
            var pinChecksMock = new Mock<IRepository<PinCheck, string>>();
            pinChecksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<PinCheck>, Task<List<PinCheck>>>>(),
                    null))
                .Returns((Func<IQueryable<PinCheck>, Task<List<PinCheck>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(pinRecords)));
            pinChecksMock.Setup(r => r.CreateAsync(It.IsAny<PinCheck>(), It.IsAny<CancellationToken>()))
                .Callback((PinCheck record, CancellationToken _) => pinRecords.Add(record))
                .Returns(Task.CompletedTask);

            dbContextMock.Setup(c => c.ChunkPins).Returns(chunkPinsMock.Object);
            dbContextMock.Setup(c => c.PinChecks).Returns(pinChecksMock.Object);
            dbContextMock.Setup(c => c.PinsChecks).Returns(pinsChecksMock.Object);

            // The lock of the pins check is granted, unless a test has another check hold it for some attempts.
            pinIntegrityServiceMock.Setup(s => s.TryAcquirePinsCheckLockAsync())
                .ReturnsAsync(() =>
                {
                    if (checkLeaseDenials > 0)
                    {
                        checkLeaseDenials--;
                        onCheckLeaseDenied?.Invoke();
                        return null;
                    }

                    var lease = new FakeResourceLockLease();
                    checkLeases.Add(lease);
                    return lease;
                });

            // The check of a pin is the integrity service's, asked as one that writes, never as a dry run: here it
            // answers per reference, unless a test has it answer otherwise. Its correction leaves a pin found
            // incomplete not succeeded, with the missing and the invalid chunks of the check, and declares succeeded
            // one found whole, with the chunks of the check.
            pinIntegrityServiceMock.Setup(s => s.CheckPinsAsync(It.IsAny<SwarmReference>(), false, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwarmReference reference, bool _, CancellationToken _) =>
                {
                    checkedReferences.Add(reference);
                    return new PinCheckResult(
                        reference,
                        3,
                        reference == ReferenceIncomplete ? [MissingChunkHash] : [],
                        reference == ReferenceInvalid ? [InvalidChunkHash] : [],
                        0);
                });
            pinIntegrityServiceMock.Setup(s => s.CorrectPinAsync(It.IsAny<ChunkPin>(), It.IsAny<PinCheckResult>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChunkPin pin, PinCheckResult checkResult, CancellationToken _) =>
                {
                    if (!checkResult.IsComplete)
                        pin.UpdateProcessed(checkResult.MissingChunks.Concat(checkResult.InvalidChunks), 0);
                    else if (pin.State != ChunkPinState.Succeeded)
                        pin.UpdateProcessed([], checkResult.TotalChunks);
                    return pin;
                });

            task = new CheckPinsTask(
                dbContextMock.Object,
                NullLogger<CheckPinsTask>.Instance,
                pinIntegrityServiceMock.Object)
            {
                CheckLeaseRetryPeriod = TimeSpan.FromMilliseconds(1)
            };
        }

        // Tests.

        [Fact]
        public async Task ChecksTheContentOfAReferenceOnceAndCorrectsEveryPinOfIt()
        {
            // Setup.
            //a content uploaded twice with its pin has two pins
            var firstPin = AddPin(ReferenceInvalid);
            var secondPin = AddPin(ReferenceInvalid);

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            //one check and one record for the reference, and each pin brought to what that check found
            Assert.Equal(1, check.TotalPins);
            Assert.Equal([ReferenceInvalid], checkedReferences);
            pinIntegrityServiceMock.Verify(
                s => s.CheckPinsAsync(ReferenceInvalid, false, It.IsAny<CancellationToken>()),
                Times.Once);
            var record = Assert.Single(pinRecords);
            Assert.Equal(PinCheckOutcome.Invalid, record.Outcome);
            Assert.Equal(1, record.InvalidChunks);
            Assert.Equal(0, record.MissingChunks);
            Assert.NotEqual(ChunkPinState.Succeeded, firstPin.State);
            Assert.NotEqual(ChunkPinState.Succeeded, secondPin.State);
            pinIntegrityServiceMock.Verify(
                s => s.CorrectPinAsync(
                    It.IsAny<ChunkPin>(),
                    It.Is<PinCheckResult>(r => r.Reference == ReferenceInvalid && r.InvalidChunks.Count == 1),
                    It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            pinIntegrityServiceMock.Verify(
                s => s.CorrectPinAsync(firstPin, It.IsAny<PinCheckResult>(), It.IsAny<CancellationToken>()),
                Times.Once);
            pinIntegrityServiceMock.Verify(
                s => s.CorrectPinAsync(secondPin, It.IsAny<PinCheckResult>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ChecksEveryPinRecordsItsOutcomeAndCorrectsThePin()
        {
            // Setup.
            //three pins declared succeeded, of which beehive holds the first in full and not the others, and one not
            //processed yet that beehive holds in full
            var completePin = AddPin(ReferenceComplete);
            var incompletePin = AddPin(ReferenceIncomplete);
            var invalidPin = AddPin(ReferenceInvalid);
            var unprocessedPin = AddPin(Reference(4), isSucceeded: false);

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            Assert.Equal(PinsCheckState.Completed, check.State);
            Assert.Equal(4, check.TotalPins);
            Assert.Equal(4, check.CheckedPins);
            Assert.Equal(2, check.CompletePins);
            Assert.Equal(1, check.IncompletePins);
            Assert.Equal(1, check.InvalidPins);
            Assert.Equal(0, check.FailedPins);
            Assert.Null(check.RunningReference);
            Assert.Equal(
                [ReferenceComplete, ReferenceIncomplete, ReferenceInvalid, Reference(4)],
                pinRecords.Select(r => r.Reference));
            Assert.Equal(
                [PinCheckOutcome.Complete, PinCheckOutcome.Incomplete, PinCheckOutcome.Invalid, PinCheckOutcome.Complete],
                pinRecords.Select(r => r.Outcome));
            Assert.Equal(
                [(3L, 0L, 0L), (3L, 1L, 0L), (3L, 0L, 1L), (3L, 0L, 0L)],
                pinRecords.Select(r => (r.TotalChunks, r.MissingChunks, r.InvalidChunks)));
            Assert.All(pinRecords, r => Assert.Same(check, r.ParentCheck));

            //every pin is brought to what its own check found: one declared succeeded and found whole stays as it is
            Assert.Equal(ChunkPinState.Succeeded, completePin.State);
            pinIntegrityServiceMock.Verify(
                s => s.CorrectPinAsync(
                    completePin,
                    It.Is<PinCheckResult>(r => r.Reference == ReferenceComplete && r.IsComplete),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            //one not succeeded and found whole is declared succeeded, with the chunks of its check
            Assert.Equal(ChunkPinState.Succeeded, unprocessedPin.State);
            Assert.Equal(3, unprocessedPin.TotPinnedChunks);
            pinIntegrityServiceMock.Verify(
                s => s.CorrectPinAsync(
                    unprocessedPin,
                    It.Is<PinCheckResult>(r => r.Reference == Reference(4) && r.IsComplete && r.TotalChunks == 3),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            //one found incomplete is not succeeded anymore
            Assert.NotEqual(ChunkPinState.Succeeded, incompletePin.State);
            Assert.Equal([MissingChunkHash], incompletePin.MissingChunks);
            Assert.NotEqual(ChunkPinState.Succeeded, invalidPin.State);
            Assert.Equal([InvalidChunkHash], invalidPin.MissingChunks);
            Assert.True(Assert.Single(checkLeases).IsReleased);
        }

        [Theory]
        [InlineData(new ChunkPinState[0], new[] { 1, 2, 3 })] //no state: the pins of every state
        [InlineData(new[] { ChunkPinState.Succeeded }, new[] { 1 })]
        [InlineData(new[] { ChunkPinState.Incomplete }, new[] { 2 })]
        [InlineData(new[] { ChunkPinState.Unprocessed }, new[] { 3 })]
        [InlineData(new[] { ChunkPinState.Incomplete, ChunkPinState.Succeeded }, new[] { 1, 2 })]
        [InlineData(new[] { ChunkPinState.Succeeded, ChunkPinState.Unprocessed }, new[] { 1, 3 })]
        [InlineData(new[] { ChunkPinState.Incomplete, ChunkPinState.Unprocessed }, new[] { 2, 3 })]
        [InlineData(new[] { ChunkPinState.Incomplete, ChunkPinState.Succeeded, ChunkPinState.Unprocessed }, new[] { 1, 2, 3 })]
        public async Task ChecksThePinsOfTheRequestedStatesOnly(ChunkPinState[] pinStates, int[] expectedReferences)
        {
            ArgumentNullException.ThrowIfNull(expectedReferences);

            // Setup.
            //a pin declared succeeded, one declared incomplete and one not processed yet
            check = NewCheck(pinStates);
            AddPin(Reference(1));
            AddPin(Reference(2)).UpdateProcessed([MissingChunkHash], 2);
            AddPin(Reference(3), isSucceeded: false);
            var declaredStates = chunkPins.Select(p => p.State).ToArray(); //before the check corrects them

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            //the db is asked for a state on the stored members: it takes the pins declaring that state
            Assert.Equal(
                [ChunkPinState.Succeeded, ChunkPinState.Incomplete, ChunkPinState.Unprocessed],
                declaredStates);
            Assert.Equal(expectedReferences.Select(Reference), checkedReferences);
            Assert.Equal(expectedReferences.Length, check.TotalPins);
            Assert.Equal(expectedReferences.Length, check.CompletePins);
        }

        [Fact]
        public async Task ChecksTheRequestedReferencesOnlyAndFailsTheOnesBeehiveDoesNotPin()
        {
            // Setup.
            check = NewCheck(requestedReferences: [ReferenceIncomplete, Reference(9)]);
            AddPin(ReferenceComplete);
            AddPin(ReferenceIncomplete);

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            Assert.Equal(PinsCheckState.Completed, check.State);
            Assert.Equal([ReferenceIncomplete], checkedReferences);
            Assert.Equal(2, check.TotalPins);
            Assert.Equal(1, check.IncompletePins);
            Assert.Equal(1, check.FailedPins);
            var failedRecord = Assert.Single(pinRecords, r => r.Reference == Reference(9));
            Assert.Equal(PinCheckOutcome.Failed, failedRecord.Outcome);
            Assert.Equal("The reference is not pinned", failedRecord.FailureReason);
        }

        [Fact]
        public async Task CountsAPinWhoseCheckThrowsAsFailedAndGoesOn()
        {
            // Setup.
            AddPin(ReferenceComplete);
            AddPin(ReferenceIncomplete);
            pinIntegrityServiceMock.Setup(s => s.CheckPinsAsync(ReferenceComplete, false, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new MongoException("connection lost"));

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            Assert.Equal(PinsCheckState.Completed, check.State);
            Assert.Equal(1, check.FailedPins);
            Assert.Equal(1, check.IncompletePins);
            Assert.Equal("connection lost", pinRecords[0].FailureReason);
            Assert.Equal(PinCheckOutcome.Incomplete, pinRecords[1].Outcome);
        }

        [Fact]
        public async Task DefersAPinLockedByAnotherOperationAndChecksItWhenReleased()
        {
            // Setup.
            //the pin, not processed yet and held in full, is locked at the first pass and free at the second one
            var lockedPin = AddPin(ReferenceComplete, isSucceeded: false);
            AddPin(ReferenceIncomplete);
            var confirmAttempts = 0;
            pinIntegrityServiceMock.Setup(s => s.CorrectPinAsync(lockedPin, It.IsAny<PinCheckResult>(), It.IsAny<CancellationToken>()))
                .Returns((ChunkPin pin, PinCheckResult _, CancellationToken _) =>
                {
                    if (++confirmAttempts == 1)
                        throw new ResourceLockException($"Pin {pin.Id} is locked by another operation");
                    lockedPin.UpdateProcessed([], 3);
                    return Task.FromResult(lockedPin);
                });

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            Assert.Equal(PinsCheckState.Completed, check.State);
            Assert.Equal(2, confirmAttempts);
            Assert.Equal(1, check.CompletePins);
            Assert.Equal(1, check.IncompletePins);
            Assert.Equal(0, check.FailedPins);
            //one record per pin, the deferred one written at its outcome, after the other one
            Assert.Equal([ReferenceIncomplete, ReferenceComplete], pinRecords.Select(r => r.Reference));
            Assert.Equal(ChunkPinState.Succeeded, lockedPin.State);
        }

        [Fact]
        public async Task DoesNothingOnACompletedCheck()
        {
            // Setup: a re-run of the job.
            AddPin(ReferenceComplete);
            check.Start(1, []);
            check.Complete();

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            Assert.Empty(checkedReferences);
            Assert.Empty(pinRecords);
        }

        [Fact]
        public async Task FailsAPinStillLockedAtTheEndOfTheCheckWithTheLockReason()
        {
            // Setup.
            var lockedPin = AddPin(ReferenceIncomplete);
            AddPin(ReferenceComplete);
            var resetAttempts = 0;
            pinIntegrityServiceMock.Setup(s => s.CorrectPinAsync(lockedPin, It.IsAny<PinCheckResult>(), It.IsAny<CancellationToken>()))
                .Returns((ChunkPin pin, PinCheckResult _, CancellationToken _) =>
                {
                    resetAttempts++;
                    throw new ResourceLockException($"Pin {pin.Id} is locked by another operation");
                });

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            Assert.Equal(PinsCheckState.Completed, check.State);
            Assert.Equal(2, resetAttempts); //the first pass and the final one
            Assert.Equal(1, check.CompletePins);
            Assert.Equal(1, check.FailedPins);
            Assert.Equal(0, check.IncompletePins);
            var lockedRecord = Assert.Single(pinRecords, r => r.Reference == ReferenceIncomplete);
            Assert.Equal(PinCheckOutcome.Failed, lockedRecord.Outcome);
            Assert.Equal($"Pin {lockedPin.Id} is locked by another operation", lockedRecord.FailureReason);
            Assert.Equal(ChunkPinState.Succeeded, lockedPin.State); //not corrected
        }

        [Fact]
        public async Task FailsTheCheckWhenThePinsCannotBeRead()
        {
            // Setup.
            var chunkPinsMock = new Mock<IRepository<ChunkPin, string>>();
            chunkPinsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<ChunkPin>, Task<List<SwarmReference>>>>(),
                    null))
                .ThrowsAsync(new MongoException("connection lost"));
            dbContextMock.Setup(c => c.ChunkPins).Returns(chunkPinsMock.Object);

            // Action.
            var exception = await Assert.ThrowsAsync<MongoException>(() => task.RunAsync(CheckId, CancellationToken.None));

            // Assert.
            Assert.Equal(PinsCheckState.Failed, check.State);
            Assert.Equal(exception.Message, check.FailureReason);
        }

        [Fact]
        public async Task GoesOnFromThePinsItRecordedWhenItsJobRunsAgain()
        {
            // Setup.
            //the shutdown arrives while the second pin is being checked
            AddPin(ReferenceComplete);
            AddPin(ReferenceIncomplete);
            AddPin(ReferenceInvalid);
            using var shutdown = new CancellationTokenSource();
            pinIntegrityServiceMock.Setup(s => s.CheckPinsAsync(ReferenceIncomplete, false, It.IsAny<CancellationToken>()))
                .Returns(async (SwarmReference _, bool _, CancellationToken cancellationToken) =>
                {
                    await shutdown.CancelAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Unreachable");
                });

            // Action.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.RunAsync(CheckId, shutdown.Token));

            // Assert.
            //the check is neither completed nor failed, nothing is recorded of the interrupted pin
            Assert.Equal(PinsCheckState.Running, check.State);
            Assert.Null(check.FailureReason);
            Assert.Equal([ReferenceComplete], pinRecords.Select(r => r.Reference));
            var startedDateTime = check.StartedDateTime;

            // Action: the re-queued job runs the check again.
            pinIntegrityServiceMock.Setup(s => s.CheckPinsAsync(ReferenceIncomplete, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    checkedReferences.Add(ReferenceIncomplete);
                    return new PinCheckResult(ReferenceIncomplete, 3, [MissingChunkHash], [], 0);
                });
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            Assert.Equal(PinsCheckState.Completed, check.State);
            Assert.Equal([ReferenceComplete, ReferenceIncomplete, ReferenceInvalid], checkedReferences); //the recorded pin is not checked again
            Assert.Equal(3, pinRecords.Count);
            Assert.Equal(1, check.CompletePins);
            Assert.Equal(1, check.IncompletePins);
            Assert.Equal(1, check.InvalidPins);
            Assert.Equal(startedDateTime, check.StartedDateTime);
        }

        [Fact]
        public async Task LeavesTheCheckUntouchedWhenCancelledWhileWaitingForItsLease()
        {
            // Setup.
            //another run holds the check, and the job of this one is cancelled while it waits
            using var shutdown = new CancellationTokenSource();
            checkLeaseDenials = 1;
            onCheckLeaseDenied = shutdown.Cancel;

            // Action.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.RunAsync(CheckId, shutdown.Token));

            // Assert.
            Assert.Null(check.StartedDateTime);
            Assert.Empty(checkLeases);
            dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StopsARunWhoseCheckLeaseIsLostWithoutWritingTheCheck()
        {
            // Setup.
            //the lease of the check is lost while the first pin is being checked: its token reaches the check
            AddPin(ReferenceComplete);
            AddPin(ReferenceIncomplete);
            pinIntegrityServiceMock.Setup(s => s.CheckPinsAsync(It.IsAny<SwarmReference>(), false, It.IsAny<CancellationToken>()))
                .Returns((SwarmReference _, bool _, CancellationToken cancellationToken) =>
                {
                    checkLeases.Single().Lose();
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Unreachable");
                });

            // Action.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.RunAsync(CheckId, CancellationToken.None));

            // Assert.
            //the check is neither completed nor failed, and nothing is written of it after the loss
            Assert.Equal(PinsCheckState.Running, check.State);
            Assert.Null(check.FailureReason);
            Assert.Empty(pinRecords);
            dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2)); //the start and the running reference, before the loss
            Assert.True(Assert.Single(checkLeases).IsReleased);
        }

        [Fact]
        public async Task StopsAtTheEndOfThePinItIsCheckingWhenAStopIsAsked()
        {
            // Setup: the stop is asked while the first pin is being checked.
            AddPin(ReferenceComplete);
            AddPin(ReferenceIncomplete);
            pinIntegrityServiceMock.Setup(s => s.CheckPinsAsync(ReferenceComplete, false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    check.RequestStop();
                    return new PinCheckResult(ReferenceComplete, 3, [], [], 0);
                });

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            Assert.Equal(PinsCheckState.Stopped, check.State);
            Assert.Equal([ReferenceComplete], pinRecords.Select(r => r.Reference)); //the pin it was on, completed
            Assert.Equal(2, check.TotalPins);
            Assert.Equal(1, check.CheckedPins);
            Assert.Null(check.RunningReference);
            Assert.Empty(checkedReferences); //the second pin is never checked
        }

        [Fact]
        public async Task WaitsForACheckHeldByAnotherRunAndRunsItOnceReleased()
        {
            // Setup.
            //the job was handed to this worker while another one still performs it: its lease is denied twice
            AddPin(ReferenceComplete);
            var startedWhileHeld = false;
            checkLeaseDenials = 2;
            onCheckLeaseDenied = () => startedWhileHeld |= check.StartedDateTime.HasValue;

            // Action.
            await task.RunAsync(CheckId, CancellationToken.None);

            // Assert.
            Assert.False(startedWhileHeld); //the check is the other run's until its lease is granted
            Assert.Equal(PinsCheckState.Completed, check.State);
            Assert.Equal(1, check.CompletePins);
            pinIntegrityServiceMock.Verify(s => s.TryAcquirePinsCheckLockAsync(), Times.Exactly(3));
            Assert.True(Assert.Single(checkLeases).IsReleased);
        }

        // Helpers.
        /// <summary>
        /// Add a pin to the db: declared succeeded with three chunks, or not processed yet
        /// </summary>
        private ChunkPin AddPin(SwarmReference reference, bool isSucceeded = true)
        {
            var pin = new ChunkPin(reference);
            ReflectionHelper.SetValue(pin, p => p.Id, Guid.NewGuid().ToString());
            ReflectionHelper.SetValue(pin, p => p.CreationDateTime, DateTime.UtcNow.AddTicks(chunkPins.Count));
            if (isSucceeded)
                pin.UpdateProcessed([], 3);
            chunkPins.Add(pin);
            return pin;
        }

        private static PinsCheck NewCheck(
            IEnumerable<ChunkPinState>? requestedPinStates = null,
            IEnumerable<SwarmReference>? requestedReferences = null)
        {
            var check = new PinsCheck(requestedPinStates, requestedReferences);
            ReflectionHelper.SetValue(check, c => c.Id, CheckId);
            return check;
        }

        private static SwarmReference Reference(int seed) => $"cc{seed:x62}";
    }
}

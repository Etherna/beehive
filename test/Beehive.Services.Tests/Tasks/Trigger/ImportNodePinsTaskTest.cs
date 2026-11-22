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
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Tasks.Trigger
{
    public class ImportNodePinsTaskTest
    {
        // Consts.
        private const string ImportId = "import-1";
        private const string NodeId = "nodeA";
        private static readonly SwarmReference ReferenceComplete = "aa00000000000000000000000000000000000000000000000000000000000001";
        private static readonly SwarmReference ReferenceIncomplete = "aa00000000000000000000000000000000000000000000000000000000000002";
        private static readonly SwarmReference ReferencePinned = "aa00000000000000000000000000000000000000000000000000000000000003";
        private static readonly SwarmHash MissingChunkHash = "bb00000000000000000000000000000000000000000000000000000000000009";

        // Fields.
        private readonly List<ChunkPin> chunkPins = [];
        private readonly Mock<IBeehiveDbContext> dbContextMock = new();
        private readonly List<FakeResourceLockLease> importLeases = [];
        private readonly BeeNodeLiveInstance node;
        private readonly Mock<ISwarmClient> nodeClientMock = new();
        private readonly List<NodePinImport> pinRecords = [];
        private readonly Mock<IPinService> pinServiceMock = new();
        private readonly ImportNodePinsTask task;
        private NodePinsImport import = NewImport(unpinImported: false);
        private int importLeaseDenials;
        private Action? onImportLeaseDenied;

        // Constructor.
        public ImportNodePinsTaskTest()
        {
            // The node under import, alive.
            node = new BeeNodeLiveInstance(NodeId, nodeClientMock.Object, true, NullLogger.Instance);
            node.Status.SucceededHeartbeatAttempt(DateTime.UtcNow);
            var beeNodeLiveManagerMock = new Mock<IBeeNodeLiveManager>();
            beeNodeLiveManagerMock.Setup(m => m.GetBeeNodeLiveInstanceAsync(NodeId)).ReturnsAsync(node);

            // The pins in memory: lookups run their predicates over them, creations get an id.
            var chunkPinsMock = new Mock<IRepository<ChunkPin, string>>();
            chunkPinsMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<ChunkPin, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<ChunkPin, bool>> predicate, CancellationToken _) =>
                    chunkPins.FirstOrDefault(predicate.Compile()));
            chunkPinsMock.Setup(r => r.CreateAsync(It.IsAny<ChunkPin>(), It.IsAny<CancellationToken>()))
                .Callback((ChunkPin pin, CancellationToken _) =>
                {
                    ReflectionHelper.SetValue(pin, p => p.Id, Guid.NewGuid().ToString());
                    chunkPins.Add(pin);
                })
                .Returns(Task.CompletedTask);
            var nodePinsImportsMock = new Mock<IRepository<NodePinsImport, string>>();
            nodePinsImportsMock.Setup(r => r.FindOneAsync(ImportId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => import);
            var nodePinImportsMock = new Mock<IRepository<NodePinImport, string>>();
            nodePinImportsMock.Setup(r => r.CreateAsync(It.IsAny<NodePinImport>(), It.IsAny<CancellationToken>()))
                .Callback((NodePinImport record, CancellationToken _) => pinRecords.Add(record))
                .Returns(Task.CompletedTask);
            dbContextMock.Setup(c => c.ChunkPins).Returns(chunkPinsMock.Object);
            dbContextMock.Setup(c => c.NodePinImports).Returns(nodePinImportsMock.Object);
            dbContextMock.Setup(c => c.NodePinsImports).Returns(nodePinsImportsMock.Object);

            // The lease of the import is granted, unless a test has another run hold it for some attempts.
            dbContextMock.Setup(c => c.TryAcquireResourceLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ResourceLockMode>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(() =>
                {
                    if (importLeaseDenials > 0)
                    {
                        importLeaseDenials--;
                        onImportLeaseDenied?.Invoke();
                        return null;
                    }

                    var lease = new FakeResourceLockLease();
                    importLeases.Add(lease);
                    return lease;
                });

            // The pinning itself is the pin service's: here it answers per reference.
            pinServiceMock.Setup(s => s.PinChunksAsync(It.IsAny<string>(), It.IsAny<BeeNodeLiveInstance?>(), It.IsAny<CancellationToken>()))
                .Returns((string pinId, BeeNodeLiveInstance? _, CancellationToken _) =>
                {
                    var pin = chunkPins.Single(p => p.Id == pinId);
                    pin.UpdateProcessed(
                        pin.Reference == ReferenceIncomplete ? [MissingChunkHash] : [],
                        totPinnedChunks: 3);
                    return Task.FromResult(new PinChunksResult(pin, []));
                });

            task = new ImportNodePinsTask(
                beeNodeLiveManagerMock.Object,
                dbContextMock.Object,
                NullLogger<ImportNodePinsTask>.Instance,
                pinServiceMock.Object)
            {
                ImportLeaseRetryPeriod = TimeSpan.FromMilliseconds(1)
            };
        }

        // Tests.

        [Fact]
        public async Task CountsAPinWhosePinningThrowsAsFailedAndGoesOn()
        {
            // Setup.
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceIncomplete, ReferenceComplete]);
            pinServiceMock.Setup(s => s.PinChunksAsync(
                    It.Is<string>(id => chunkPins.Single(p => p.Id == id).Reference == ReferenceIncomplete),
                    It.IsAny<BeeNodeLiveInstance?>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Pin doesn't have an hash"));

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(NodePinsImportState.Completed, import.State);
            Assert.Equal(1, import.FailedPins);
            Assert.Equal(1, import.ImportedPins);
            var failedRecord = Assert.Single(pinRecords, r => r.Reference == ReferenceIncomplete);
            Assert.Equal(NodePinImportOutcome.Failed, failedRecord.Outcome);
            Assert.Equal("Pin doesn't have an hash", failedRecord.FailureReason);
        }

        [Fact]
        public async Task DefersAPinLockedByAnotherOperationAndImportsItWhenReleased()
        {
            // Setup.
            //the pin is locked at the first pass and free at the second one
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete, ReferencePinned]);
            AddSucceededPin(ReferencePinned);
            var pinningAttempts = 0;
            pinServiceMock.Setup(s => s.PinChunksAsync(
                    It.Is<string>(id => chunkPins.Single(p => p.Id == id).Reference == ReferenceComplete),
                    It.IsAny<BeeNodeLiveInstance?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string pinId, BeeNodeLiveInstance? _, CancellationToken _) =>
                {
                    if (++pinningAttempts == 1)
                        throw new ResourceLockException($"Pin {pinId} is locked by another operation");
                    var pin = chunkPins.Single(p => p.Id == pinId);
                    pin.UpdateProcessed([], totPinnedChunks: 3);
                    return Task.FromResult(new PinChunksResult(pin, []));
                });

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(NodePinsImportState.Completed, import.State);
            Assert.Equal(2, pinningAttempts);
            Assert.Equal(1, import.ImportedPins);
            Assert.Equal(1, import.SkippedPins);
            Assert.Equal(0, import.FailedPins);
            //one record per pin, the deferred one written at its outcome, after the other ones
            Assert.Equal([ReferencePinned, ReferenceComplete], pinRecords.Select(r => r.Reference));
            Assert.Equal(NodePinImportOutcome.Imported, pinRecords[1].Outcome);
        }

        [Fact]
        public async Task FailsAPinStillLockedAtTheEndOfTheImportWithTheLockReason()
        {
            // Setup.
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete, ReferenceIncomplete]);
            var pinningAttempts = 0;
            pinServiceMock.Setup(s => s.PinChunksAsync(
                    It.Is<string>(id => chunkPins.Single(p => p.Id == id).Reference == ReferenceComplete),
                    It.IsAny<BeeNodeLiveInstance?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string pinId, BeeNodeLiveInstance? _, CancellationToken _) =>
                {
                    pinningAttempts++;
                    throw new ResourceLockException($"Pin {pinId} is locked by another operation");
                });

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(NodePinsImportState.Completed, import.State);
            Assert.Equal(2, pinningAttempts); //the first pass and the final one
            Assert.Equal(2, import.FailedPins);
            var lockedRecord = Assert.Single(pinRecords, r => r.Reference == ReferenceComplete);
            Assert.Equal(NodePinImportOutcome.Failed, lockedRecord.Outcome);
            Assert.Equal(
                $"Pin {chunkPins.Single(p => p.Reference == ReferenceComplete).Id} is locked by another operation",
                lockedRecord.FailureReason);
        }

        [Fact]
        public async Task LeavesAnImportCancelledByAShutdownRunningForItsRequeuedJob()
        {
            // Setup.
            //the shutdown arrives while the first pin is being pinned
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete, ReferenceIncomplete]);
            using var shutdown = new CancellationTokenSource();
            pinServiceMock.Setup(s => s.PinChunksAsync(
                    It.Is<string>(id => chunkPins.Single(p => p.Id == id).Reference == ReferenceComplete),
                    It.IsAny<BeeNodeLiveInstance?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (string _, BeeNodeLiveInstance? _, CancellationToken cancellationToken) =>
                {
                    await shutdown.CancelAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Unreachable");
                });

            // Action.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.RunAsync(ImportId, shutdown.Token));

            // Assert.
            //the import is neither completed nor failed, nothing is recorded of the interrupted pin
            Assert.Equal(NodePinsImportState.Running, import.State);
            Assert.Null(import.FailureReason);
            Assert.Empty(pinRecords);
            pinServiceMock.Verify(s => s.PinChunksAsync(It.IsAny<string>(), It.IsAny<BeeNodeLiveInstance?>(), It.IsAny<CancellationToken>()), Times.Once);

            // Action: the re-queued job runs the import again.
            pinServiceMock.Setup(s => s.PinChunksAsync(
                    It.Is<string>(id => chunkPins.Single(p => p.Id == id).Reference == ReferenceComplete),
                    It.IsAny<BeeNodeLiveInstance?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string pinId, BeeNodeLiveInstance? _, CancellationToken _) =>
                {
                    var pin = chunkPins.Single(p => p.Id == pinId);
                    pin.UpdateProcessed([], totPinnedChunks: 3);
                    return Task.FromResult(new PinChunksResult(pin, []));
                });
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(NodePinsImportState.Completed, import.State);
            Assert.Equal(1, import.ImportedPins);
            Assert.Equal(1, import.FailedPins);
            Assert.Equal(2, pinRecords.Count);
        }

        [Fact]
        public async Task LeavesTheImportUntouchedWhenCancelledWhileWaitingForItsLease()
        {
            // Setup.
            //another run holds the import, and the job of this one is cancelled while it waits
            using var shutdown = new CancellationTokenSource();
            importLeaseDenials = 1;
            onImportLeaseDenied = shutdown.Cancel;

            // Action.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.RunAsync(ImportId, shutdown.Token));

            // Assert.
            Assert.Null(import.StartedDateTime);
            Assert.Empty(importLeases);
            dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StopsARunWhoseImportLeaseIsLostWithoutWritingTheImport()
        {
            // Setup.
            //the lease of the import is lost while the first pin is being pinned: its token reaches the pinning
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete, ReferenceIncomplete]);
            pinServiceMock.Setup(s => s.PinChunksAsync(
                    It.Is<string>(id => chunkPins.Single(p => p.Id == id).Reference == ReferenceComplete),
                    It.IsAny<BeeNodeLiveInstance?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string _, BeeNodeLiveInstance? _, CancellationToken cancellationToken) =>
                {
                    importLeases.Single().Lose();
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new InvalidOperationException("Unreachable");
                });

            // Action.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.RunAsync(ImportId, CancellationToken.None));

            // Assert.
            //the import is neither completed nor failed, and nothing is written of it after the loss
            Assert.Equal(NodePinsImportState.Running, import.State);
            Assert.Null(import.FailureReason);
            Assert.Empty(pinRecords);
            pinServiceMock.Verify(s => s.PinChunksAsync(It.IsAny<string>(), It.IsAny<BeeNodeLiveInstance?>(), It.IsAny<CancellationToken>()), Times.Once);
            dbContextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2)); //the start and the total, before the loss
            Assert.True(Assert.Single(importLeases).IsReleased);
        }

        [Fact]
        public async Task WaitsForAnImportHeldByAnotherRunAndRunsItOnceReleased()
        {
            // Setup.
            //the job was handed to this worker while another one still performs it: its lease is denied twice
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete]);
            var startedWhileHeld = false;
            importLeaseDenials = 2;
            onImportLeaseDenied = () => startedWhileHeld |= import.StartedDateTime.HasValue;

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.False(startedWhileHeld); //the import is the other run's until its lease is granted
            Assert.Equal(NodePinsImportState.Completed, import.State);
            Assert.Equal(1, import.ImportedPins);
            dbContextMock.Verify(c => c.TryAcquireResourceLockAsync(
                    ResourceLockNamespaces.NodePinsImports,
                    ImportId,
                    ResourceLockMode.Exclusive,
                    null),
                Times.Exactly(3));
            Assert.True(Assert.Single(importLeases).IsReleased);
        }

        [Fact]
        public async Task DoesNothingOnACompletedImport()
        {
            // Setup.
            import.Start();
            import.Complete();

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            nodeClientMock.Verify(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task FailsTheImportWhenTheNodeCannotListItsPins()
        {
            // Setup.
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SwarmSdkApiException(
                    "The HTTP status code of the response was not expected (503).",
                    503,
                    """{"code":503,"message":"syncing in progress"}""",
                    new Dictionary<string, IEnumerable<string>>(),
                    null));

            // Action & assert.
            await Assert.ThrowsAsync<SwarmSdkApiException>(() => task.RunAsync(ImportId, CancellationToken.None));
            Assert.Equal(NodePinsImportState.Failed, import.State);
            Assert.NotNull(import.FailureReason);
            Assert.Null(import.TotalPins);
            Assert.True(Assert.Single(importLeases).IsReleased); //the lease of the import is released with the failure
        }

        [Fact]
        public async Task ImportsNewPinsAndSkipsThePinnedOnes()
        {
            // Setup.
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete, ReferencePinned, ReferenceIncomplete]);
            AddSucceededPin(ReferencePinned);

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(NodePinsImportState.Completed, import.State);
            Assert.Equal(3, import.TotalPins);
            Assert.Equal(1, import.ImportedPins);
            Assert.Equal(1, import.SkippedPins);
            Assert.Equal(1, import.FailedPins);
            Assert.Equal(0, import.UnpinnedPins);
            Assert.True(chunkPins.Single(p => p.Reference == ReferenceComplete).IsSucceeded);
            Assert.False(chunkPins.Single(p => p.Reference == ReferenceIncomplete).IsSucceeded);
            pinServiceMock.Verify(s => s.PinChunksAsync(It.IsAny<string>(), node, It.IsAny<CancellationToken>()), Times.Exactly(2)); //read from the node itself
            nodeClientMock.Verify(c => c.DeletePinAsync(It.IsAny<SwarmReference>(), It.IsAny<CancellationToken>()), Times.Never);

            //one record per pin, with its outcome
            Assert.Equal(3, pinRecords.Count);
            Assert.All(pinRecords, r => Assert.Same(import, r.Import));
            Assert.All(pinRecords, r => Assert.Equal(NodeId, r.NodeId));
            var importedRecord = Assert.Single(pinRecords, r => r.Reference == ReferenceComplete);
            Assert.Equal(NodePinImportOutcome.Imported, importedRecord.Outcome);
            Assert.Equal(3, importedRecord.PinnedChunks);
            Assert.Null(importedRecord.FailureReason);
            var skippedRecord = Assert.Single(pinRecords, r => r.Reference == ReferencePinned);
            Assert.Equal(NodePinImportOutcome.AlreadyPinned, skippedRecord.Outcome);
            var failedRecord = Assert.Single(pinRecords, r => r.Reference == ReferenceIncomplete);
            Assert.Equal(NodePinImportOutcome.Failed, failedRecord.Outcome);
            Assert.Equal("1 chunks missing on the node", failedRecord.FailureReason);
            Assert.Equal([MissingChunkHash], failedRecord.MissingChunks);
            Assert.All(pinRecords, r => Assert.False(r.UnpinnedByImport));
        }

        [Fact]
        public async Task ResumesAnInterruptedImportFromZero()
        {
            // Setup.
            import.Start();
            import.PinImported();
            import.PinImported();
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete]);

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(NodePinsImportState.Completed, import.State);
            Assert.Equal(1, import.TotalPins);
            Assert.Equal(1, import.ImportedPins);
        }

        [Fact]
        public async Task UnpinsOnTheNodeOnlyTheContentsBeehiveHoldsInFull()
        {
            // Setup.
            import = NewImport(unpinImported: true);
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete, ReferencePinned, ReferenceIncomplete]);
            AddSucceededPin(ReferencePinned);

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(2, import.UnpinnedPins);
            nodeClientMock.Verify(c => c.DeletePinAsync(ReferenceComplete, It.IsAny<CancellationToken>()), Times.Once);
            nodeClientMock.Verify(c => c.DeletePinAsync(ReferencePinned, It.IsAny<CancellationToken>()), Times.Once);
            nodeClientMock.Verify(c => c.DeletePinAsync(ReferenceIncomplete, It.IsAny<CancellationToken>()), Times.Never);
            Assert.True(Assert.Single(pinRecords, r => r.Reference == ReferenceComplete).UnpinnedByImport);
            Assert.True(Assert.Single(pinRecords, r => r.Reference == ReferencePinned).UnpinnedByImport);
            Assert.False(Assert.Single(pinRecords, r => r.Reference == ReferenceIncomplete).UnpinnedByImport);
        }

        [Fact]
        public async Task TreatsAContentWithInvalidChunksAsIncompleteAndKeepsItOnTheNode()
        {
            // Setup.
            import = NewImport(unpinImported: true);
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete]);
            pinServiceMock.Setup(s => s.PinChunksAsync(It.IsAny<string>(), It.IsAny<BeeNodeLiveInstance?>(), It.IsAny<CancellationToken>()))
                .Returns((string pinId, BeeNodeLiveInstance? _, CancellationToken _) =>
                {
                    var pin = chunkPins.Single(p => p.Id == pinId);
                    pin.UpdateProcessed([], totPinnedChunks: 3);
                    return Task.FromResult(new PinChunksResult(pin, [MissingChunkHash]));
                });

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(1, import.FailedPins);
            Assert.Equal(0, import.UnpinnedPins);
            var record = Assert.Single(pinRecords);
            Assert.Equal(NodePinImportOutcome.Failed, record.Outcome);
            Assert.Equal("1 invalid chunks on the node", record.FailureReason);
            Assert.Equal([MissingChunkHash], record.InvalidChunks);
            nodeClientMock.Verify(c => c.DeletePinAsync(It.IsAny<SwarmReference>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RecordsAnUnpinFailureWithoutFailingTheImport()
        {
            // Setup.
            import = NewImport(unpinImported: true);
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete]);
            nodeClientMock.Setup(c => c.DeletePinAsync(ReferenceComplete, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SwarmSdkApiException(
                    "The HTTP status code of the response was not expected (500).",
                    500,
                    """{"code":500,"message":"unpin failed"}""",
                    new Dictionary<string, IEnumerable<string>>(),
                    null));

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(NodePinsImportState.Completed, import.State);
            Assert.Equal(1, import.ImportedPins);
            Assert.Equal(0, import.UnpinnedPins);
            var record = Assert.Single(pinRecords);
            Assert.Equal(NodePinImportOutcome.Imported, record.Outcome);
            Assert.False(record.UnpinnedByImport);
            Assert.Equal("unpin failed", record.UnpinFailureReason);
        }

        [Fact]
        public async Task ImportsTheRequestedReferencesOnlyWithoutListingTheNode()
        {
            // Setup.
            import = NewImport(unpinImported: false, requestedReferences: [ReferenceComplete, ReferenceIncomplete]);
            nodeClientMock.Setup(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync([ReferenceComplete, ReferencePinned, ReferenceIncomplete]);

            // Action.
            await task.RunAsync(ImportId, CancellationToken.None);

            // Assert.
            Assert.Equal(NodePinsImportState.Completed, import.State);
            Assert.Equal(2, import.TotalPins);
            Assert.Equal(1, import.ImportedPins);
            Assert.Equal(1, import.FailedPins);
            Assert.Equal([ReferenceComplete, ReferenceIncomplete], pinRecords.Select(r => r.Reference));
            nodeClientMock.Verify(c => c.GetAllPinsAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        // Helpers.
        private static NodePinsImport NewImport(bool unpinImported, IEnumerable<SwarmReference>? requestedReferences = null)
        {
            var import = new NodePinsImport(NodeId, unpinImported, requestedReferences);
            ReflectionHelper.SetValue(import, i => i.Id, ImportId);
            return import;
        }

        private void AddSucceededPin(SwarmReference reference)
        {
            var pin = new ChunkPin(reference);
            ReflectionHelper.SetValue(pin, p => p.Id, Guid.NewGuid().ToString());
            pin.UpdateProcessed([], 3);
            chunkPins.Add(pin);
        }
    }
}

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
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Trigger
{
    public class ImportNodePinsTask(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        ILogger<ImportNodePinsTask> logger,
        IPinIntegrityService pinIntegrityService,
        IPinService pinService)
        : IImportNodePinsTask
    {
        // Properties.
        /// <summary>
        /// How long a run waits before asking again for an import held by another run
        /// </summary>
        internal TimeSpan ImportLeaseRetryPeriod { get; init; } = TimeSpan.FromSeconds(5);

        // Methods.
        public async Task RunAsync(string nodePinsImportId, CancellationToken cancellationToken)
        {
            // One run at a time works on an import, under its exclusive lease. Hangfire can hand the job to a second
            // worker while the first is still performing it, and cancels the first only afterwards: the new run
            // waits for the other one to leave, or for its lease to expire when it is dead.
            IResourceLockLease? acquiredLease;
            var isWaiting = false;
            while ((acquiredLease = await dbContext.TryAcquireResourceLockAsync(
                       ResourceLockNamespaces.NodePinsImports,
                       nodePinsImportId)) is null)
            {
                if (!isWaiting)
                    logger.NodePinsImportHeldByAnotherRun(nodePinsImportId);
                isWaiting = true;
                await Task.Delay(ImportLeaseRetryPeriod, cancellationToken);
            }
            await using var importLease = acquiredLease;
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                importLease.LeaseLostToken);
            var runToken = runCancellation.Token;

            var import = await dbContext.NodePinsImports.FindOneAsync(nodePinsImportId, runToken);
            if (import.IsCompleted) //a re-run of the job
                return;

            import.Start();
            await dbContext.SaveChangesAsync(runToken);
            logger.NodePinsImportStarted(import.NodeId, import.UnpinImported);

            try
            {
                var node = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(import.NodeId);

                var references = import.ImportsEveryPin ?
                    await node.Client.GetAllPinsAsync(runToken) :
                    [.. import.RequestedReferences];
                import.SetTotalPins(references.Length);
                await dbContext.SaveChangesAsync(runToken);

                // First pass: a pin locked by another operation is deferred to the end of the run.
                List<SwarmReference> deferredReferences = [];
                foreach (var reference in references)
                {
                    if (!await TryImportPinAsync(import, node, reference, deferOnLock: true, runToken))
                        deferredReferences.Add(reference);
                    await dbContext.SaveChangesAsync(runToken); //the progress is readable while the import runs
                }

                // Second pass: a pin still locked fails with that reason.
                foreach (var reference in deferredReferences)
                {
                    await TryImportPinAsync(import, node, reference, deferOnLock: false, runToken);
                    await dbContext.SaveChangesAsync(runToken);
                }

                import.Complete();
            }
            catch (OperationCanceledException) when (importLease.LeaseLostToken.IsCancellationRequested)
            {
                // The lease is lost: the import is not this run's to write anymore.
                logger.NodePinsImportLeaseLost(import.NodeId, import.Id);
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The job is cancelled, by a shutdown or because another worker took it: the import stays running,
                // and the next run of the job starts it again.
                logger.NodePinsImportInterrupted(import.NodeId, import.ImportedPins, import.FailedPins);
                await dbContext.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception e)
            {
                import.Fail(e.Message);
                await dbContext.SaveChangesAsync(CancellationToken.None);
                logger.NodePinsImportFailed(import.NodeId, e);
                throw;
            }

            await dbContext.SaveChangesAsync(CancellationToken.None);
            logger.NodePinsImportCompleted(
                import.NodeId,
                import.ImportedPins,
                import.SkippedPins,
                import.FailedPins,
                import.UnpinnedPins);
        }

        // Helpers.
        private static string DescribeIncompleteImport(int missingChunks, int invalidChunks, string location) =>
            (missingChunks, invalidChunks) switch
            {
                (> 0, > 0) => $"{missingChunks} chunks missing and {invalidChunks} invalid {location}",
                (> 0, _) => $"{missingChunks} chunks missing {location}",
                _ => $"{invalidChunks} invalid chunks {location}"
            };

        /// <summary>
        /// Import one pin of the node, recording its outcome
        /// </summary>
        /// <param name="deferOnLock">Whether a pin locked by another operation is left for a later pass, without a record</param>
        /// <returns>False when the pin was deferred, true when it got its outcome</returns>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A pin failing to import must not stop the import of the other ones")]
        private async Task<bool> TryImportPinAsync(
            NodePinsImport import,
            BeeNodeLiveInstance node,
            SwarmReference reference,
            bool deferOnLock,
            CancellationToken cancellationToken)
        {
            var record = new NodePinImport(import, node.Id, reference);

            // Find the beehive pin of the reference, or create it.
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Reference == reference, cancellationToken);
            if (pin is null)
            {
                pin = new ChunkPin(reference);
                await dbContext.ChunkPins.CreateAsync(pin, cancellationToken);
            }

            // Pin the chunks read from the node, unless beehive already holds them all. A pin declared succeeded is
            // checked on beehive's own chunks first: one found incomplete is reset, and imported again.
            bool isComplete;
            try
            {
                if (pin.IsSucceeded)
                {
                    var pinnedCheckResult = await pinIntegrityService.CheckPinAsync(pin, cancellationToken);
                    if (pinnedCheckResult.IsComplete)
                    {
                        record.SetAlreadyPinned(pinnedCheckResult.TotalChunks);
                        import.PinSkipped();
                    }
                    else
                    {
                        pin = await pinIntegrityService.ResetPinAsync(pin.Id, pinnedCheckResult, cancellationToken);
                    }
                }

                isComplete = pin.IsSucceeded;
                if (!isComplete)
                {
                    var result = await pinService.PinChunksAsync(pin.Id, node, cancellationToken); //the chunks are read from the node itself
                    pin = result.Pin;
                    IReadOnlyCollection<SwarmHash> missingChunks = [.. pin.MissingChunks];
                    var invalidChunks = result.InvalidChunks;
                    var location = "on the node";

                    // The content is held only when its check finds it whole on beehive's own chunks.
                    if (pin.IsSucceeded && invalidChunks.Count == 0)
                    {
                        var checkResult = await pinIntegrityService.CheckPinAsync(pin, cancellationToken);
                        if (!checkResult.IsComplete)
                        {
                            pin = await pinIntegrityService.ResetPinAsync(pin.Id, checkResult, cancellationToken);
                            missingChunks = checkResult.MissingChunks;
                            invalidChunks = checkResult.InvalidChunks;
                            location = "in beehive after the import";
                        }
                    }

                    isComplete = pin.IsSucceeded && invalidChunks.Count == 0;
                    if (isComplete)
                    {
                        record.SetImported(pin.TotPinnedChunks);
                        import.PinImported();
                    }
                    else
                    {
                        logger.NodePinImportIncomplete(node.Id, reference, missingChunks.Count, invalidChunks.Count);
                        record.SetFailed(
                            DescribeIncompleteImport(missingChunks.Count, invalidChunks.Count, location),
                            missingChunks,
                            invalidChunks,
                            pin.TotPinnedChunks);
                        import.PinFailed();
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; //a shutdown, not a failure of the pin
            }
            catch (ResourceLockException) when (deferOnLock)
            {
                logger.NodePinImportDeferred(node.Id, reference);
                return false;
            }
            catch (Exception e)
            {
                logger.NodePinImportFailed(node.Id, reference, e);
                record.SetFailed(e.Message);
                import.PinFailed();
                await dbContext.NodePinImports.CreateAsync(record, cancellationToken);
                return true;
            }

            // Unpin on the node, only a content beehive holds in full.
            if (import.UnpinImported && isComplete)
            {
                try
                {
                    await node.Client.DeletePinAsync(reference, cancellationToken);
                    record.SetUnpinnedByImport();
                    import.PinUnpinned();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; //a shutdown, not a failure of the node
                }
                catch (Exception e) when (e.IsBeeNodeCallFailure())
                {
                    logger.NodePinUnpinFailed(node.Id, reference, e);
                    record.SetUnpinFailed(e.GetBeeNodeFailureReason());
                }
            }

            await dbContext.NodePinImports.CreateAsync(record, cancellationToken);
            return true;
        }
    }
}

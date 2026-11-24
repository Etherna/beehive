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
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Trigger
{
    /// <summary>
    /// The pins check as a background run: the check <c>GET /pins/check</c> answers as a stream, recorded. It checks
    /// the integrity of beehive's own pins, a pinned reference at a time, recording the outcome of each one:
    /// the pins of the requested references, or the ones of the requested states. Every pin is corrected to what its
    /// check finds, and nothing else is written: one found whole is declared succeeded when it wasn't; one found
    /// incomplete goes back to not succeeded with its missing chunks, and its invalid chunks are deleted
    /// </summary>
    public class CheckPinsTask(
        IBeehiveDbContext dbContext,
        ILogger<CheckPinsTask> logger,
        IPinIntegrityService pinIntegrityService)
        : ICheckPinsTask
    {
        // Properties.
        /// <summary>
        /// How long a run waits before asking again for a check held by another run
        /// </summary>
        internal TimeSpan CheckLeaseRetryPeriod { get; init; } = TimeSpan.FromSeconds(5);

        // Methods.
        public async Task RunAsync(string pinsCheckId, CancellationToken cancellationToken)
        {
            // One check of many pins runs at a time, under the exclusive lease of the pins check: a run of this job,
            // or the stream of a check of every pin (GET /pins/check). Hangfire can hand the job to a second worker
            // while the first is still performing it, and cancels the first only afterwards: the new run waits for
            // the holder to leave, or for its lease to expire when it is dead.
            IResourceLockLease? acquiredLease;
            var isWaiting = false;
            while ((acquiredLease = await pinIntegrityService.TryAcquirePinsCheckLockAsync()) is null)
            {
                if (!isWaiting)
                    logger.PinsCheckHeldByAnotherRun(pinsCheckId);
                isWaiting = true;
                await Task.Delay(CheckLeaseRetryPeriod, cancellationToken);
            }
            await using var checkLease = acquiredLease;
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                checkLease.LeaseLostToken);
            var runToken = runCancellation.Token;

            var check = await dbContext.PinsChecks.FindOneAsync(pinsCheckId, runToken);
            if (check.IsCompleted) //a re-run of the job
                return;

            try
            {
                // The references to check: the requested ones, or the ones of the pins of the requested states, of
                // every state when none is requested, by the age of their first pin. Every pin is in one state, so
                // the pins of the requested states are the ones left without the pins of each other state. The
                // state of a pin is computed (ChunkPin.State): the db is asked for it on the members it is read from.
                ChunkPinState[] pinStates = check.RequestedPinStates.Any() ?
                    [.. check.RequestedPinStates] :
                    Enum.GetValues<ChunkPinState>();
                SwarmReference[] references = check.ChecksRequestedReferences ?
                    [.. check.RequestedReferences] :
                    [.. (await dbContext.ChunkPins.QueryElementsAsync(elements =>
                    {
                        var pins = elements.Where(p => p.Reference.HasValue);
                        if (!pinStates.Contains(ChunkPinState.Incomplete))
                            pins = pins.Where(p => !p.IsProcessed || !p.MissingChunks.Any());
                        if (!pinStates.Contains(ChunkPinState.Succeeded))
                            pins = pins.Where(p => !p.IsProcessed || p.MissingChunks.Any());
                        if (!pinStates.Contains(ChunkPinState.Unprocessed))
                            pins = pins.Where(p => p.IsProcessed);
                        return pins.OrderBy(p => p.CreationDateTime)
                            .Select(p => p.Reference!.Value)
                            .ToListAsync(runToken);
                    })).Distinct()];

                // A run started again, after its job was interrupted, goes on from the pins it recorded.
                HashSet<SwarmReference> recordedReferences;
                using (dbContext.StartTransientModelsScope())
                {
                    var records = await dbContext.PinChecks.QueryElementsAsync(elements =>
                        elements.Where(r => r.ParentCheck.Id == pinsCheckId)
                            .ToListAsync(runToken));
                    recordedReferences = [.. records.Select(r => r.Reference)];
                    check.Start(references.Length, records.Select(r => r.Outcome));
                }
                await dbContext.SaveChangesAsync(runToken);
                logger.PinsCheckStarted(check.Id, references.Length, recordedReferences.Count);

                // First pass: a pin locked by another operation is deferred to the end of the run. Second pass: a
                // pin still locked fails with that reason. A pass ends before its end on a stop request.
                var deferredReferences = await CheckPinsAsync(
                    check,
                    references.Where(r => !recordedReferences.Contains(r)),
                    deferOnLock: true,
                    runToken);
                if (deferredReferences is not null &&
                    await CheckPinsAsync(check, deferredReferences, deferOnLock: false, runToken) is not null)
                    check.Complete();
                else
                    check.StopOnRequest();
            }
            catch (OperationCanceledException) when (checkLease.LeaseLostToken.IsCancellationRequested)
            {
                // The lease is lost: the check is not this run's to write anymore.
                logger.PinsCheckLeaseLost(check.Id);
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The job is cancelled, by a shutdown or because another worker took it: the check stays running,
                // and the next run of the job goes on with it.
                logger.PinsCheckInterrupted(check.Id, check.CheckedPins);
                await dbContext.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception e)
            {
                check.Fail(e.Message);
                await dbContext.SaveChangesAsync(CancellationToken.None);
                logger.PinsCheckFailed(check.Id, e);
                throw;
            }

            await dbContext.SaveChangesAsync(CancellationToken.None);
            if (check.IsStopped)
                logger.PinsCheckStopped(check.Id, check.CheckedPins, check.TotalPins!.Value);
            else
                logger.PinsCheckCompleted(
                    check.Id,
                    check.CompletePins,
                    check.IncompletePins,
                    check.InvalidPins,
                    check.FailedPins);
        }

        // Helpers.
        /// <summary>
        /// Check the pins of some references, one after the other, until a stop is asked
        /// </summary>
        /// <param name="deferOnLock">Whether a pin locked by another operation is left for a later pass, without a record</param>
        /// <returns>The references left for a later pass, or null when a stop was asked</returns>
        private async Task<List<SwarmReference>?> CheckPinsAsync(
            PinsCheck check,
            IEnumerable<SwarmReference> references,
            bool deferOnLock,
            CancellationToken cancellationToken)
        {
            List<SwarmReference> deferredReferences = [];
            foreach (var reference in references)
            {
                var stopRequests = await dbContext.PinsChecks.QueryElementsAsync(elements =>
                    elements.Where(c => c.Id == check.Id)
                        .Select(c => c.StopRequestedDateTime)
                        .ToListAsync(cancellationToken));
                if (stopRequests.Any(r => r.HasValue))
                    return null;

                check.SetRunningReference(reference);
                await dbContext.SaveChangesAsync(cancellationToken); //the progress is readable while the check runs

                // The pins, the records and the chunks read for a reference leave the db context with it: a check
                // of every pin keeps none of them in memory.
                using (dbContext.StartTransientModelsScope())
                {
                    if (!await TryCheckPinAsync(check, reference, deferOnLock, cancellationToken))
                        deferredReferences.Add(reference);
                }
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return deferredReferences;
        }

        /// <summary>
        /// Check the pins of a reference and correct them, recording the outcome
        /// </summary>
        /// <param name="deferOnLock">Whether a pin locked by another operation is left for a later pass, without a record</param>
        /// <returns>False when the reference was deferred, true when it got its outcome</returns>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A pin failing its check must not stop the check of the other ones")]
        private async Task<bool> TryCheckPinAsync(
            PinsCheck check,
            SwarmReference reference,
            bool deferOnLock,
            CancellationToken cancellationToken)
        {
            var record = new PinCheck(check, reference);
            try
            {
                // A content uploaded more than once with its pin has a pin per upload: the content is checked once,
                // and each pin of it is brought to what the check finds.
                var pins = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                    elements.Where(p => p.Reference == reference)
                        .OrderBy(p => p.CreationDateTime)
                        .ToListAsync(cancellationToken));
                if (pins.Count == 0)
                {
                    record.SetFailed("The reference is not pinned");
                }
                else
                {
                    var checkResult = await pinIntegrityService.CheckPinsAsync(reference, dryRun: false, cancellationToken);
                    foreach (var pin in pins)
                        await pinIntegrityService.CorrectPinAsync(pin, checkResult, cancellationToken);

                    record.SetChecked(
                        checkResult.TotalChunks,
                        checkResult.MissingChunks.Count,
                        checkResult.InvalidChunks.Count);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; //a shutdown, not a failure of the pin
            }
            catch (ResourceLockException) when (deferOnLock)
            {
                logger.PinCheckDeferred(reference);
                return false;
            }
            catch (Exception e)
            {
                logger.PinCheckFailed(reference, e);
                record.SetFailed(e.Message);
            }

            check.PinChecked(record.Outcome);
            await dbContext.PinChecks.CreateAsync(record, cancellationToken);
            return true;
        }
    }
}

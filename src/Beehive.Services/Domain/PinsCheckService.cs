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
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Tasks.Trigger;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Extensions;
using Etherna.SwarmSdk.Models;
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public sealed class PinsCheckService(
        IBackgroundJobClient backgroundJobClient,
        IBeehiveDbContext dbContext,
        IHangfireJobsMonitor hangfireJobsMonitor,
        ILogger<PinsCheckService> logger,
        IPinIntegrityService pinIntegrityService)
        : IPinsCheckService
    {
        // Methods.
        public async Task<long> DeleteChecksAsync(bool? failed, CancellationToken cancellationToken = default)
        {
            // The pin records of a deleted check follow it, by the delete policy of their reference to it.
            var checks = await dbContext.PinsChecks.QueryElementsAsync(elements =>
                elements.Where(c => c.CompletedDateTime != null)
                    .Where(c => failed == null || (failed.Value ? c.FailureReason != null : c.FailureReason == null))
                    .ToListAsync(cancellationToken));
            foreach (var check in checks)
                await dbContext.PinsChecks.DeleteAsync(check, cancellationToken: cancellationToken);
            logger.PinsChecksDeleted(checks.Count);

            return checks.Count;
        }

        public async Task<PinsCheck> GetCheckAsync(string id, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(id);

            var check = await dbContext.PinsChecks.FindOneAsync(id, cancellationToken);
            await FailIfJobIsGoneAsync(check);

            return check;
        }

        public async Task<IReadOnlyList<PinsCheck>> GetChecksAsync(
            int page,
            int take,
            CancellationToken cancellationToken = default)
        {
            var checks = await dbContext.PinsChecks.QueryElementsAsync(elements =>
                elements.PaginateDescending(c => c.CreationDateTime, page, take)
                    .ToListAsync(cancellationToken));

            foreach (var check in checks)
                await FailIfJobIsGoneAsync(check);

            return checks;
        }

        public async Task<PinsCheck> StartCheckAsync(
            IEnumerable<ChunkPinState>? pinStates = null,
            IEnumerable<SwarmReference>? references = null,
            CancellationToken cancellationToken = default)
        {
            var check = new PinsCheck(pinStates, references);

            // A check reads the chunks keyed by hash only: not while a deprecated store holds chunks.
            await pinIntegrityService.VerifyPinsCanBeCheckedAsync(cancellationToken);

            // One check of many pins at a time, whichever way it runs: not while a run is running, nor while the
            // stream of a check of every pin (GET /pins/check) is being written, which holds the pins check lock.
            var latestCheck = await TryGetLatestCheckAsync(cancellationToken);
            if (latestCheck is not null && !latestCheck.IsCompleted)
                throw new ResourceLockException("A run of the pins check is still running");
            var pinsCheckLease = await pinIntegrityService.TryAcquirePinsCheckLockAsync() ??
                throw new ResourceLockException("A check of every pin is running");
            await pinsCheckLease.DisposeAsync(); //the run takes it when its job starts

            // Record the check, then run it in background. Once recorded, the writes complete whatever the request
            // does: a check without its job id would never be found gone.
            await dbContext.PinsChecks.CreateAsync(check, CancellationToken.None);

            var jobId = backgroundJobClient.Enqueue<ICheckPinsTask>(t => t.RunAsync(check.Id, CancellationToken.None));
            check.SetJobId(jobId);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            logger.PinsCheckEnqueued(check.Id, jobId, check.RequestedPinStates, check.RequestedReferences.Count());

            return check;
        }

        public async Task<PinsCheck> StopCheckAsync(string id, CancellationToken cancellationToken = default)
        {
            var check = await GetCheckAsync(id, cancellationToken);
            if (check.IsCompleted)
                return check;

            check.RequestStop();
            await dbContext.SaveChangesAsync(CancellationToken.None);

            return check;
        }

        public async Task<PinsCheck?> TryGetLatestCheckAsync(CancellationToken cancellationToken = default)
        {
            var check = (await dbContext.PinsChecks.QueryElementsAsync(elements =>
                elements.OrderByDescending(c => c.CreationDateTime)
                    .Take(1)
                    .ToListAsync(cancellationToken))).FirstOrDefault();
            if (check is null)
                return null;

            await FailIfJobIsGoneAsync(check);

            return check;
        }

        // Helpers.
        /// <summary>
        /// A check whose job is gone without completing it (a server stopped mid-way) will never complete: fail it.
        /// The pins it checked keep their correction, a new check takes the others
        /// </summary>
        private async Task FailIfJobIsGoneAsync(PinsCheck check)
        {
            if (check.IsCompleted || check.JobId is null || hangfireJobsMonitor.IsJobActive(check.JobId))
                return;

            check.Fail("The check job ended before completing the check");
            await dbContext.SaveChangesAsync();
            logger.PinsCheckJobGone(check.Id, check.JobId);
        }
    }
}

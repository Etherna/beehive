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

using Hangfire;
using Hangfire.States;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.Beehive.Services.Utilities
{
    internal sealed class HangfireJobsMonitor : IHangfireJobsMonitor
    {
        // Consts.
        private static readonly HashSet<string> ActiveStateNames =
        [
            AwaitingState.StateName,
            EnqueuedState.StateName,
            ProcessingState.StateName,
            ScheduledState.StateName
        ];

        // Methods.
        public bool IsJobActive(string jobId)
        {
            ArgumentNullException.ThrowIfNull(jobId);

            var jobDetails = JobStorage.Current.GetMonitoringApi().JobDetails(jobId);
            var currentState = jobDetails?.History
                .OrderByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            return currentState is not null && ActiveStateNames.Contains(currentState.StateName);
        }
    }
}

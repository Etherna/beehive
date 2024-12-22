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
using Hangfire;
using Hangfire.Storage.Monitoring;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks
{
    public class CleanupOldFailedTasksTask(ILogger<CleanupOldFailedTasksTask> logger)
        : ICleanupOldFailedTasksTask
    {
        // Consts.
        public const string TaskId = "cleanupOldFailedTasksTask";
        
        // Fields.
        private readonly TimeSpan retentionTime = new(30, 0, 0, 0);

        // Methods.
        public Task RunAsync()
        {
            var api = JobStorage.Current.GetMonitoringApi();
            var tasksToSkip = 0;
            var deletedTasks = 0L;
            JobList<FailedJobDto> failedJobs;

            // Log begin.
            logger.CleanupOldFailedTasksTaskStarted();

            // Run clean up.
            do
            {
                failedJobs = api.FailedJobs(tasksToSkip, 1000 /* limit */);
                
                foreach (var job in failedJobs)
                {
                    if (job.Value.FailedAt is not null &&
                        DateTime.UtcNow - job.Value.FailedAt > retentionTime)
                    {
                        BackgroundJob.Delete(job.Key);
                        deletedTasks++;
                    }
                    else
                        tasksToSkip++;
                }
            } while (failedJobs.Count > 0);

            // Log result.
            logger.CleanupOldFailedTasksTaskCompleted(deletedTasks);

            return Task.CompletedTask;
        }
    }
}
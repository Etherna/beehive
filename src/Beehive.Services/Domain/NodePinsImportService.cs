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
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Tasks.Trigger;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Extensions;
using Etherna.SwarmSdk.Models;
using Hangfire;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public sealed class NodePinsImportService(
        IBackgroundJobClient backgroundJobClient,
        IBeehiveDbContext dbContext,
        IBeeNodeLiveManager beeNodeLiveManager,
        IHangfireJobsMonitor hangfireJobsMonitor,
        ILogger<NodePinsImportService> logger)
        : INodePinsImportService
    {
        // Methods.
        public async Task<long> DeleteImportsAsync(string nodeId, bool? failed)
        {
            ArgumentNullException.ThrowIfNull(nodeId);

            // The pin records of a deleted import follow it, by the delete policy of their reference to it.
            var imports = await dbContext.NodePinsImports.QueryElementsAsync(elements =>
                elements.Where(i => i.NodeId == nodeId && i.CompletedDateTime != null)
                    .Where(i => failed == null || (failed.Value ? i.FailureReason != null : i.FailureReason == null))
                    .ToListAsync());
            foreach (var import in imports)
                await dbContext.NodePinsImports.DeleteAsync(import);
            logger.NodePinsImportsDeleted(nodeId, imports.Count);

            return imports.Count;
        }

        public async Task<IReadOnlyList<NodePinsImport>> GetImportsAsync(int page, int take)
        {
            var imports = await dbContext.NodePinsImports.QueryElementsAsync(elements =>
                elements.PaginateDescending(i => i.CreationDateTime, page, take)
                    .ToListAsync());

            foreach (var import in imports)
                await FailIfJobIsGoneAsync(import);

            return imports;
        }

        public async Task<NodePinsSnapshot> GetPinsSnapshotAsync(string nodeId)
        {
            ArgumentNullException.ThrowIfNull(nodeId);

            var node = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);
            var references = await node.Client.GetAllPinsAsync();
            if (references.Length == 0)
                return new NodePinsSnapshot([], []);

            var pinnedReferences = await dbContext.ChunkPins.QueryElementsAsync(elements =>
                elements.Where(p => p.Reference.HasValue && references.Contains(p.Reference!.Value))
                    .Where(p => p.IsProcessed && !p.MissingChunks.Any())
                    .Select(p => p.Reference!.Value)
                    .ToListAsync());

            return new NodePinsSnapshot(references, [.. references.Except(pinnedReferences)]);
        }

        public async Task<NodePinsImport> StartImportAsync(
            string nodeId,
            bool unpinImported,
            IEnumerable<SwarmReference>? references = null)
        {
            ArgumentNullException.ThrowIfNull(nodeId);

            // One import at a time per node.
            var latestImport = await TryGetLatestImportAsync(nodeId);
            if (latestImport is not null && !latestImport.IsCompleted)
                throw new ResourceLockException($"An import of the pins of node {nodeId} is still running");

            // Record the import, then run it in background.
            var import = new NodePinsImport(nodeId, unpinImported, references);
            await dbContext.NodePinsImports.CreateAsync(import);

            var jobId = backgroundJobClient.Enqueue<IImportNodePinsTask>(t => t.RunAsync(import.Id));
            import.SetJobId(jobId);
            await dbContext.SaveChangesAsync();
            logger.NodePinsImportEnqueued(nodeId, jobId, unpinImported, import.RequestedReferences.Count());

            return import;
        }

        public async Task<NodePinsImport?> TryGetLatestImportAsync(string nodeId)
        {
            ArgumentNullException.ThrowIfNull(nodeId);

            var import = (await dbContext.NodePinsImports.QueryElementsAsync(elements =>
                elements.Where(i => i.NodeId == nodeId)
                    .OrderByDescending(i => i.CreationDateTime)
                    .Take(1)
                    .ToListAsync())).FirstOrDefault();
            if (import is null)
                return null;

            await FailIfJobIsGoneAsync(import);

            return import;
        }

        // Helpers.
        /// <summary>
        /// An import whose job is gone without completing it (a server stopped mid-way) will never complete: fail it
        /// </summary>
        private async Task FailIfJobIsGoneAsync(NodePinsImport import)
        {
            if (import.IsCompleted || import.JobId is null || hangfireJobsMonitor.IsJobActive(import.JobId))
                return;

            import.Fail("The import job ended before completing the import");
            await dbContext.SaveChangesAsync();
            logger.NodePinsImportJobGone(import.NodeId, import.Id, import.JobId);
        }
    }
}

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
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver.Linq;
using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Pages.Admin
{
    public class PinsImportsModel(
        IBeehiveDbContext dbContext,
        IBeeNodeLiveManager beeNodeLiveManager,
        INodePinsImportService nodePinsImportService)
        : PageModel
    {
        // Consts.
        public const int MaxListedRecords = 200;

        // Properties.
        [BindProperty(SupportsGet = true)]
        public bool FailedOnly { get; set; }

        [TempData]
        public string? Message { get; set; }

        public IReadOnlyList<PinsImportsNodeView> Nodes { get; private set; } = [];

        // Methods.
        public async Task OnGetAsync()
        {
            List<PinsImportsNodeView> nodes = [];
            foreach (var node in beeNodeLiveManager.AllNodes.OrderBy(n => n.Id))
            {
                // The contents still on the node: a node not alive is not asked.
                NodePinsSnapshot? pinsSnapshot = null;
                var nodeError = node.Status.NotAliveReason;
                if (nodeError is null)
                {
                    try
                    {
                        pinsSnapshot = await nodePinsImportService.GetPinsSnapshotAsync(node.Id);
                    }
                    catch (Exception e) when (e.IsBeeNodeCallFailure())
                    {
                        nodeError = e.GetBeeNodeFailureReason();
                    }
                }

                // The running or last import, with its pin records.
                var latestImport = await nodePinsImportService.TryGetLatestImportAsync(node.Id);
                List<NodePinImport> records = [];
                if (latestImport is not null)
                {
                    var failedOnly = FailedOnly;
                    records = await dbContext.NodePinImports.QueryElementsAsync(elements =>
                        elements.Where(r => r.Import.Id == latestImport.Id)
                            .Where(r => !failedOnly || r.FailureReason != null)
                            .OrderByDescending(r => r.CreationDateTime)
                            .Take(MaxListedRecords)
                            .ToListAsync());
                }

                nodes.Add(new PinsImportsNodeView(
                    node.Id,
                    node.Client.NodeUrl,
                    node.Status.IsAlive,
                    pinsSnapshot,
                    nodeError,
                    latestImport,
                    records));
            }

            Nodes = nodes;
        }

        public async Task<IActionResult> OnPostCleanupAsync(string nodeId, bool? failed)
        {
            ArgumentNullException.ThrowIfNull(nodeId);

            var deletedImports = await nodePinsImportService.DeleteImportsAsync(nodeId, failed);
            Message = $"Node {nodeId}: {deletedImports} {(failed switch { true => "failed", false => "completed", null => "finished" })} imports deleted, with their pin records";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostStartAsync(string nodeId, bool unpin, string? references)
        {
            ArgumentNullException.ThrowIfNull(nodeId);

            try
            {
                // The references typed in, one per line: none for every pin of the node.
                var requestedReferences = (references ?? "")
                    .Split(['\n', '\r', ' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(r => new SwarmReference(r))
                    .ToArray();

                var import = await nodePinsImportService.StartImportAsync(nodeId, unpin, requestedReferences);
                Message = $"Node {nodeId}: pins import started, job {import.JobId}" +
                    (requestedReferences.Length > 0 ? $", {requestedReferences.Length} requested contents" : "");
            }
            catch (ResourceLockException)
            {
                Message = $"Node {nodeId}: an import is still running";
            }
            catch (ArgumentException e)
            {
                Message = $"Node {nodeId}: a reference is not valid, {e.Message}";
            }

            return RedirectToPage();
        }
    }
}

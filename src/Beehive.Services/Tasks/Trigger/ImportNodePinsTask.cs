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
    public class ImportNodePinsTask(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        ILogger<ImportNodePinsTask> logger,
        IPinService pinService)
        : IImportNodePinsTask
    {
        // Methods.
        public async Task RunAsync(string nodePinsImportId, CancellationToken cancellationToken)
        {
            var import = await dbContext.NodePinsImports.FindOneAsync(nodePinsImportId, cancellationToken);
            if (import.IsCompleted) //a re-run of the job
                return;

            import.Start();
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.NodePinsImportStarted(import.NodeId, import.UnpinImported);

            try
            {
                var node = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(import.NodeId);

                var references = import.ImportsEveryPin ?
                    await node.Client.GetAllPinsAsync(cancellationToken) :
                    [.. import.RequestedReferences];
                import.SetTotalPins(references.Length);
                await dbContext.SaveChangesAsync(cancellationToken);

                // First pass: a pin locked by another operation is deferred to the end of the run.
                List<SwarmReference> deferredReferences = [];
                foreach (var reference in references)
                {
                    if (!await TryImportPinAsync(import, node, reference, deferOnLock: true, cancellationToken))
                        deferredReferences.Add(reference);
                    await dbContext.SaveChangesAsync(cancellationToken); //the progress is readable while the import runs
                }

                // Second pass: a pin still locked fails with that reason.
                foreach (var reference in deferredReferences)
                {
                    await TryImportPinAsync(import, node, reference, deferOnLock: false, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                import.Complete();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A shutdown: the import stays running, and the re-queued job starts it again.
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
        private static string DescribeIncompleteImport(int missingChunks, int invalidChunks) =>
            (missingChunks, invalidChunks) switch
            {
                (> 0, > 0) => $"{missingChunks} chunks missing and {invalidChunks} invalid on the node",
                (> 0, _) => $"{missingChunks} chunks missing on the node",
                _ => $"{invalidChunks} invalid chunks on the node"
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

            // Pin the chunks read from the node, unless beehive already holds them all.
            var isComplete = pin.IsSucceeded;
            if (isComplete)
            {
                record.SetAlreadyPinned(pin.TotPinnedChunks);
                import.PinSkipped();
            }
            else
            {
                PinChunksResult result;
                try
                {
                    result = await pinService.PinChunksAsync(pin.Id, node, cancellationToken); //the chunks are read from the node itself
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

                pin = result.Pin;
                isComplete = pin.IsSucceeded && result.InvalidChunks.Count == 0;
                if (isComplete)
                {
                    record.SetImported(pin.TotPinnedChunks);
                    import.PinImported();
                }
                else
                {
                    var missingChunks = pin.MissingChunks.ToArray();
                    logger.NodePinImportIncomplete(node.Id, reference, missingChunks.Length, result.InvalidChunks.Count);
                    record.SetFailed(
                        DescribeIncompleteImport(missingChunks.Length, result.InvalidChunks.Count),
                        missingChunks,
                        result.InvalidChunks,
                        pin.TotPinnedChunks);
                    import.PinFailed();
                }
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

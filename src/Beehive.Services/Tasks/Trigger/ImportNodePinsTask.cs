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
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks.Trigger
{
    public class ImportNodePinsTask(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        ILogger<ImportNodePinsTask> logger,
        IPinService pinService,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IImportNodePinsTask
    {
        // Methods.
        public async Task RunAsync(string nodePinsImportId)
        {
            var import = await dbContext.NodePinsImports.FindOneAsync(nodePinsImportId);
            if (import.IsCompleted) //a re-run of the job
                return;

            import.Start();
            await dbContext.SaveChangesAsync();
            logger.NodePinsImportStarted(import.NodeId, import.UnpinImported);

            try
            {
                var node = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(import.NodeId);

                var references = import.ImportsEveryPin ?
                    await node.Client.GetAllPinsAsync() :
                    [.. import.RequestedReferences];
                import.SetTotalPins(references.Length);
                await dbContext.SaveChangesAsync();

                foreach (var reference in references)
                {
                    await ImportPinAsync(import, node, reference);
                    await dbContext.SaveChangesAsync(); //the progress is readable while the import runs
                }

                import.Complete();
            }
            catch (Exception e)
            {
                import.Fail(e.Message);
                await dbContext.SaveChangesAsync();
                logger.NodePinsImportFailed(import.NodeId, e);
                throw;
            }

            await dbContext.SaveChangesAsync();
            logger.NodePinsImportCompleted(
                import.NodeId,
                import.ImportedPins,
                import.SkippedPins,
                import.FailedPins,
                import.UnpinnedPins);
        }

        // Helpers.
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A pin failing to import must not stop the import of the other ones")]
        private async Task ImportPinAsync(NodePinsImport import, BeeNodeLiveInstance node, SwarmReference reference)
        {
            var record = new NodePinImport(import, node.Id, reference);

            // Find the beehive pin of the reference, or create it.
            var pin = await dbContext.ChunkPins.TryFindOneAsync(p => p.Reference == reference);
            if (pin is null)
            {
                pin = new ChunkPin(reference);
                await dbContext.ChunkPins.CreateAsync(pin);
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
                    await using var chunkStore = new BeehiveChunkStore(
                        beeNodeLiveManager,
                        dbContext,
                        logger,
                        serializerModifierAccessor,
                        sourceNode: node);
                    result = await pinService.PinChunksAsync(pin.Id, chunkStore);
                }
                catch (Exception e)
                {
                    logger.NodePinImportFailed(node.Id, reference, e);
                    record.SetFailed(e.Message);
                    import.PinFailed();
                    await dbContext.NodePinImports.CreateAsync(record);
                    return;
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
                    await node.Client.DeletePinAsync(reference);
                    record.SetUnpinnedByImport();
                    import.PinUnpinned();
                }
                catch (Exception e) when (e.IsBeeNodeCallFailure())
                {
                    logger.NodePinUnpinFailed(node.Id, reference, e);
                    record.SetUnpinFailed(e.GetBeeNodeFailureReason());
                }
            }

            await dbContext.NodePinImports.CreateAsync(record);
        }

        private static string DescribeIncompleteImport(int missingChunks, int invalidChunks) =>
            (missingChunks, invalidChunks) switch
            {
                (> 0, > 0) => $"{missingChunks} chunks missing and {invalidChunks} invalid on the node",
                (> 0, _) => $"{missingChunks} chunks missing on the node",
                _ => $"{invalidChunks} invalid chunks on the node"
            };
    }
}

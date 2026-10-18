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

using Etherna.Beehive.Areas.Api.DtoModels;
using Etherna.Beehive.Areas.Api.InputModels;
using Etherna.Beehive.Configs;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Extensions;
using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api
{
    public sealed class BeehiveApiHandler(
        IBeehiveDbContext beehiveDbContext,
        IBeeNodeLiveManager beeNodeLiveManager,
        ILogger<BeehiveApiHandler> logger,
        INodePinsImportService nodePinsImportService)
        : IBeehiveApiHandler
    {
        public Task<IResult> AddBeeNodeAsync(BeeNodeInput nodeInput) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                ArgumentNullException.ThrowIfNull(nodeInput);

                // Create node.
                var node = new BeeNode(
                    new Uri(nodeInput.ConnectionString, UriKind.Absolute),
                    nodeInput.EnableBatchCreation);
                await beehiveDbContext.BeeNodes.CreateAsync(node);

                logger.NodeRegistered(
                    node.Id,
                    node.ConnectionString,
                    node.IsBatchCreationEnabled);

                return Results.Created();
            });

        public Task<IResult> DeleteNodePinsImportsAsync(string id, bool? failed) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                ArgumentNullException.ThrowIfNull(id);

                var deletedImports = await nodePinsImportService.DeleteImportsAsync(id, failed);

                return Results.Json(
                    deletedImports,
                    CommonConsts.BeehiveV04JsonSerializerOptions);
            });

        public Task<IResult> FindByIdAsync(string id) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                var node = await beehiveDbContext.BeeNodes.FindOneAsync(id);
                var nodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(node.Id);

                return Results.Json(
                    new BeeNodeDto(
                        node.Id,
                        node.ConnectionString,
                        nodeInstance.Status.Errors,
                        nodeInstance.Status.Addresses?.Ethereum,
                        nodeInstance.Status.HeartbeatTimeStamp,
                        nodeInstance.Status.IsAlive,
                        node.IsBatchCreationEnabled,
                        nodeInstance.Status.Addresses?.Overlay,
                        nodeInstance.Status.Addresses?.PssPublicKey,
                        nodeInstance.Status.Addresses?.PublicKey),
                    CommonConsts.BeehiveV04JsonSerializerOptions);
            });

        public Task<IResult> GetBeeNodesAsync() =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                var nodeDtos = new List<BeeNodeDto>();
            
                var nodes = await beehiveDbContext.BeeNodes.QueryElementsAsync(elements =>
                    elements.ToListAsync());

                foreach (var node in nodes)
                {
                    var nodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(node.Id);
                    nodeDtos.Add(new BeeNodeDto(
                        node.Id,
                        node.ConnectionString,
                        nodeInstance.Status.Errors,
                        nodeInstance.Status.Addresses?.Ethereum,
                        nodeInstance.Status.HeartbeatTimeStamp,
                        nodeInstance.Status.IsAlive,
                        node.IsBatchCreationEnabled,
                        nodeInstance.Status.Addresses?.Overlay,
                        nodeInstance.Status.Addresses?.PssPublicKey,
                        nodeInstance.Status.Addresses?.PublicKey));
                }

                return Results.Json(
                    nodeDtos,
                    CommonConsts.BeehiveV04JsonSerializerOptions);
            });

        public Task<IResult> GetNodeNotImportedPinsAsync(string id) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                ArgumentNullException.ThrowIfNull(id);

                var snapshot = await nodePinsImportService.GetPinsSnapshotAsync(id);

                return Results.Json(
                    snapshot.NotImported,
                    CommonConsts.BeehiveV04JsonSerializerOptions);
            });

        public Task<IResult> GetNodePinImportsAsync(string id, string importId, bool failedOnly, int page, int take) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                ArgumentNullException.ThrowIfNull(id);
                ArgumentNullException.ThrowIfNull(importId);

                var records = await beehiveDbContext.NodePinImports.QueryElementsAsync(elements =>
                    elements.Where(r => r.NodeId == id && r.Import.Id == importId)
                        .Where(r => !failedOnly || r.FailureReason != null)
                        .PaginateDescending(r => r.CreationDateTime, page, take)
                        .ToListAsync());

                // What the node holds right now, when it can be asked.
                var pinnedOnNode = await TryGetPinnedOnNodeAsync(id);

                return Results.Json(
                    records.Select(r => BuildNodePinImportDto(r, pinnedOnNode?.Contains(r.Reference))),
                    CommonConsts.BeehiveV04JsonSerializerOptions);
            });

        public Task<IResult> GetNodePinsImportsAsync(string id, int page, int take) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                ArgumentNullException.ThrowIfNull(id);

                var imports = await beehiveDbContext.NodePinsImports.QueryElementsAsync(elements =>
                    elements.Where(i => i.NodeId == id)
                        .PaginateDescending(i => i.CreationDateTime, page, take)
                        .ToListAsync());

                return Results.Json(
                    imports.Select(BuildNodePinsImportDto),
                    CommonConsts.BeehiveV04JsonSerializerOptions);
            });

        public Task<IResult> GetNodesPinsImportsAsync(int page, int take) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                var imports = await nodePinsImportService.GetImportsAsync(page, take);

                return Results.Json(
                    imports.Select(BuildNodePinsImportDto),
                    CommonConsts.BeehiveV04JsonSerializerOptions);
            });

        public Task<IResult> RemoveBeeNodeAsync(string id) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                ArgumentNullException.ThrowIfNull(id);

                await beehiveDbContext.BeeNodes.DeleteAsync(id);

                logger.NodeRemoved(id);
                
                return Results.Ok();
            });

        public Task<IResult> StartNodePinsImportAsync(string id, bool unpin, NodePinsImportInput? importInput) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                ArgumentNullException.ThrowIfNull(id);

                var node = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id); //not found when not registered
                var references = (importInput?.References ?? []).Select(r => new SwarmReference(r)).ToArray(); //400 on a bad one
                var import = await nodePinsImportService.StartImportAsync(node.Id, unpin, references);

                return Results.Json(
                    BuildNodePinsImportDto(import),
                    CommonConsts.BeehiveV04JsonSerializerOptions,
                    statusCode: StatusCodes.Status202Accepted);
            });

        public Task<IResult> UpdateBeeNodeAsync(string id, BeeNodeInput nodeInput) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                ArgumentNullException.ThrowIfNull(id);
                ArgumentNullException.ThrowIfNull(nodeInput);

                // Update live instance and db config.
                var nodeDb = await beehiveDbContext.BeeNodes.FindOneAsync(id);
                var nodeLiveInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);

                nodeDb.ConnectionString = new Uri(nodeInput.ConnectionString, UriKind.Absolute);
            
                nodeLiveInstance.IsBatchCreationEnabled = nodeInput.EnableBatchCreation;
                nodeDb.IsBatchCreationEnabled = nodeInput.EnableBatchCreation;

                // Update config.
                await beehiveDbContext.SaveChangesAsync();
            
                await beeNodeLiveManager.TryRemoveBeeNodeAsync(id);
                await beeNodeLiveManager.TryAddBeeNodeAsync(nodeDb);

                logger.NodeConfigurationUpdated(id);
                
                return Results.Ok();
            });

        // Helpers.
        private static NodePinImportDto BuildNodePinImportDto(NodePinImport record, bool? isOnNode) =>
            new(record.CreationDateTime,
                record.FailureReason,
                record.Id,
                record.Import.Id,
                record.InvalidChunks,
                isOnNode,
                record.MissingChunks,
                record.NodeId,
                record.Outcome,
                record.PinnedChunks,
                record.Reference,
                record.UnpinFailureReason,
                record.UnpinnedByImport);

        /// <summary>
        /// The contents pinned on a node right now: null when the node is not alive, or didn't answer
        /// </summary>
        private async Task<HashSet<SwarmReference>?> TryGetPinnedOnNodeAsync(string nodeId)
        {
            var node = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);
            if (!node.Status.IsAlive)
                return null;

            try
            {
                return [.. await node.Client.GetAllPinsAsync()];
            }
            catch (Exception e) when (e.IsBeeNodeCallFailure())
            {
                return null;
            }
        }

        private static NodePinsImportDto BuildNodePinsImportDto(NodePinsImport import) =>
            new(import.CompletedDateTime,
                import.CreationDateTime,
                import.FailureReason,
                import.FailedPins,
                import.Id,
                import.ImportedPins,
                import.JobId,
                import.NodeId,
                import.RequestedReferences,
                import.SkippedPins,
                import.StartedDateTime,
                import.State,
                import.TotalPins,
                import.UnpinImported,
                import.UnpinnedPins);
    }
}
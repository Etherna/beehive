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
using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api
{
    public sealed class BeehiveApiHandler(
        IBeehiveDbContext beehiveDbContext,
        IBeeNodeLiveManager beeNodeLiveManager,
        ILogger<BeehiveApiHandler> logger)
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

        public Task<IResult> FindByIdAsync(string id) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                var node = await beehiveDbContext.BeeNodes.FindOneAsync(id);
                var nodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(node.Id);

                return Results.Json(new BeeNodeDto(
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

                return Results.Json(nodeDtos);
            });

        public Task<IResult> RemoveBeeNodeAsync(string id) =>
            ExceptionHandler.RunAsync(ApiVersion.BeehiveV04, async () =>
            {
                ArgumentNullException.ThrowIfNull(id);

                await beehiveDbContext.BeeNodes.DeleteAsync(id);

                logger.NodeRemoved(id);
                
                return Results.Ok();
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
            
                beeNodeLiveManager.TryRemoveBeeNode(id);
                await beeNodeLiveManager.TryAddBeeNodeAsync(nodeDb);

                logger.NodeConfigurationUpdated(id);
                
                return Results.Ok();
            });
    }
}
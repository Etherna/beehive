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

using Etherna.Beehive.Areas.Api.V0_4.DtoModels;
using Etherna.Beehive.Areas.Api.V0_4.InputModels;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver.Linq;
using Etherna.MongODM.Core.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.V0_4.Services
{
    public class NodesControllerService(
        IBeehiveDbContext beehiveDbContext,
        IBeeNodeLiveManager beeNodeLiveManager,
        ILogger<NodesControllerService> logger) :
        INodesControllerService
    {
        public async Task<BeeNodeDto> AddBeeNodeAsync(BeeNodeInput nodeInput)
        {
            ArgumentNullException.ThrowIfNull(nodeInput, nameof(nodeInput));

            // Create node.
            var node = new BeeNode(
                new Uri(nodeInput.ConnectionString, UriKind.Absolute),
                nodeInput.EnableBatchCreation);
            await beehiveDbContext.BeeNodes.CreateAsync(node);

            logger.NodeRegistered(
                node.Id,
                node.ConnectionString,
                node.IsBatchCreationEnabled);

            return new BeeNodeDto(
                node.Id,
                node.ConnectionString,
                [],
                null,
                new DateTime(0),
                false,
                node.IsBatchCreationEnabled,
                null,
                null,
                null);
        }

        public async Task<BeeNodeDto> FindByIdAsync(string id)
        {
            var node = await beehiveDbContext.BeeNodes.FindOneAsync(id);
            var nodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(node.Id);
            
            return new BeeNodeDto(
                node.Id,
                node.ConnectionString,
                nodeInstance.Status.Errors,
                nodeInstance.Status.Addresses?.Ethereum,
                nodeInstance.Status.HeartbeatTimeStamp,
                nodeInstance.Status.IsAlive,
                node.IsBatchCreationEnabled,
                nodeInstance.Status.Addresses?.Overlay,
                nodeInstance.Status.Addresses?.PssPublicKey,
                nodeInstance.Status.Addresses?.PublicKey);
        }

        public async Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take)
        {
            var nodeDtos = new List<BeeNodeDto>();
            
            var nodes = await beehiveDbContext.BeeNodes.QueryElementsAsync(elements =>
                elements.PaginateDescending(n => n.CreationDateTime, page, take)
                    .ToListAsync());

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

            return nodeDtos;
        }

        public async Task RemoveBeeNodeAsync(string id)
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            await beehiveDbContext.BeeNodes.DeleteAsync(id);

            logger.NodeRemoved(id);
        }

        public async Task UpdateBeeNodeAsync(string id, BeeNodeInput nodeInput)
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            ArgumentNullException.ThrowIfNull(nodeInput, nameof(nodeInput));

            // Update live instance and db config.
            var nodeDb = await beehiveDbContext.BeeNodes.FindOneAsync(id);
            var nodeLiveInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);

            nodeDb.ConnectionString = new Uri(nodeInput.ConnectionString, UriKind.Absolute);
            
            nodeLiveInstance.IsBatchCreationEnabled = nodeInput.EnableBatchCreation;
            nodeDb.IsBatchCreationEnabled = nodeInput.EnableBatchCreation;

            // Update config.
            await beehiveDbContext.SaveChangesAsync();
            
            beeNodeLiveManager.RemoveBeeNode(id);
            await beeNodeLiveManager.AddBeeNodeAsync(nodeDb);

            logger.NodeConfigurationUpdated(id);
        }
    }
}
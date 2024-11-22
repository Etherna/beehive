// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.InputModels;
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Extensions;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Exceptions;
using Etherna.BeeNet.Models;
using Etherna.MongoDB.Driver.Linq;
using Etherna.MongODM.Core.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    internal sealed class NodesControllerService(
        IBeehiveDbContext beehiveDbContext,
        IBeeNodeLiveManager beeNodeLiveManager,
        ILogger<NodesControllerService> logger)
        : INodesControllerService
    {
        // Methods.
        public async Task<BeeNodeDto> AddBeeNodeAsync(BeeNodeInput input)
        {
            ArgumentNullException.ThrowIfNull(input, nameof(input));

            // Create node.
            var node = new BeeNode(
                input.ConnectionScheme,
                input.GatewayApiPort,
                input.Hostname,
                input.EnableBatchCreation);
            await beehiveDbContext.BeeNodes.CreateAsync(node);

            logger.NodeRegistered(
                node.Id,
                node.BaseUrl,
                node.GatewayPort,
                node.IsBatchCreationEnabled);

            return new BeeNodeDto(node);
        }

        public async Task<bool> CheckResourceAvailabilityFromNodeAsync(string id, SwarmHash hash)
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            return await beeNodeInstance.Client.IsContentRetrievableAsync(hash);
        }

        public async Task DeletePinAsync(string id, SwarmHash hash)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            await beeNodeInstance.RemovePinnedResourceAsync(hash);
        }

        public async Task<BeeNodeDto> FindByIdAsync(string id) =>
            new BeeNodeDto(await beehiveDbContext.BeeNodes.FindOneAsync(id));

        public async Task<bool> ForceFullStatusRefreshAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            return await beeNodeInstance.TryRefreshStatusAsync(true);
        }

        public IEnumerable<BeeNodeStatusDto> GetAllBeeNodeLiveStatus() =>
            beeNodeLiveManager.AllNodes.Select(n => new BeeNodeStatusDto(n.Id, n.Status));

        public async Task<BeeNodeStatusDto> GetBeeNodeLiveStatusAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            return new BeeNodeStatusDto(beeNodeInstance.Id, beeNodeInstance.Status);
        }

        public async Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take) =>
            (await beehiveDbContext.BeeNodes.QueryElementsAsync(elements =>
                elements.PaginateDescending(n => n.CreationDateTime, page, take)
                        .ToListAsync()))
            .Select(n => new BeeNodeDto(n));

        public async Task<PinnedResourceDto> GetPinDetailsAsync(string id, SwarmHash hash)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);

            if (await beeNodeInstance.IsPinningResourceAsync(hash))
                return new PinnedResourceDto(hash, id, PinnedResourceStatusDto.Pinned);
            else if (beeNodeInstance.InProgressPins.Contains(hash))
                return new PinnedResourceDto(hash, id, PinnedResourceStatusDto.InProgress);
            else
                return new PinnedResourceDto(hash, id, PinnedResourceStatusDto.NotPinned);
        }

        public async Task<IEnumerable<SwarmHash>> GetPinsByNodeAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            var readyPins = await beeNodeInstance.Client.GetAllPinsAsync();
            var inProgressPins = beeNodeInstance.InProgressPins;
            return readyPins.Union(inProgressPins);
        }

        public async Task<PostageBatchDto> GetPostageBatchDetailsAsync(string id, PostageBatchId batchId)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            try
            {
                var postageBatch = await beeNodeInstance.Client.GetPostageBatchAsync(batchId);
                return new PostageBatchDto(postageBatch);
            }
            catch (BeeNetApiException ex) when (ex.StatusCode == 400)
            {
                throw new KeyNotFoundException();
            }
        }

        public async Task<IEnumerable<PostageBatchDto>> GetPostageBatchesByNodeAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            var batches = await beeNodeInstance.Client.GetOwnedPostageBatchesByNodeAsync();
            return batches.Select(b => new PostageBatchDto(b));
        }

        public async Task NotifyPinningOfUploadedContentAsync(string id, SwarmHash hash)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            beeNodeInstance.NotifyPinnedResource(hash);
        }

        public async Task RemoveBeeNodeAsync(string id)
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            await beehiveDbContext.BeeNodes.DeleteAsync(id);

            logger.NodeRemoved(id);
        }

        public async Task ReuploadResourceToNetworkFromNodeAsync(string id, SwarmHash hash)
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));

            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            await beeNodeInstance.Client.ReuploadContentAsync(hash);
        }

        public async Task UpdateNodeConfigAsync(string id, UpdateNodeConfigInput newConfig)
        {
            ArgumentNullException.ThrowIfNull(id, nameof(id));
            ArgumentNullException.ThrowIfNull(newConfig, nameof(newConfig));

            // Update live instance and db config.
            bool rebuildLiveInstance = false;
            var nodeLiveInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            var nodeDb = await beehiveDbContext.BeeNodes.FindOneAsync(id);

            if (newConfig.ConnectionScheme != null)
            {
                rebuildLiveInstance = true;
                nodeDb.ConnectionScheme = newConfig.ConnectionScheme;
            }

            if (newConfig.EnableBatchCreation != null)
            {
                nodeLiveInstance.IsBatchCreationEnabled = newConfig.EnableBatchCreation.Value;
                nodeDb.IsBatchCreationEnabled = newConfig.EnableBatchCreation.Value;
            }

            if (newConfig.ApiPort != null)
            {
                rebuildLiveInstance = true;
                nodeDb.GatewayPort = newConfig.ApiPort.Value;
            }

            if (newConfig.Hostname != null)
            {
                rebuildLiveInstance = true;
                nodeDb.Hostname = newConfig.Hostname;
            }

            // Update config on db.
            await beehiveDbContext.SaveChangesAsync();
            
            // Rebuild live instance if necessary (changed connection string)
            if (rebuildLiveInstance)
            {
                beeNodeLiveManager.RemoveBeeNode(id);
                await beeNodeLiveManager.AddBeeNodeAsync(nodeDb);
            }

            logger.NodeConfigurationUpdated(id);
        }
    }
}

//   Copyright 2021-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.InputModels;
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Extensions;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Exceptions;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core.Extensions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class NodesControllerService : INodesControllerService
    {
        // Fields.
        private readonly IBeehiveDbContext beehiveDbContext;
        private readonly IBeeNodeLiveManager beeNodeLiveManager;
        private readonly ILogger<NodesControllerService> logger;

        // Constructor.
        public NodesControllerService(
            IBeehiveDbContext beehiveDbContext,
            IBeeNodeLiveManager beeNodeLiveManager,
            ILogger<NodesControllerService> logger)
        {
            this.beehiveDbContext = beehiveDbContext;
            this.beeNodeLiveManager = beeNodeLiveManager;
            this.logger = logger;
        }

        // Methods.
        public async Task<BeeNodeDto> AddBeeNodeAsync(BeeNodeInput input)
        {
            if (input is null)
                throw new ArgumentNullException(nameof(input));

            // Create node.
            var node = new BeeNode(
                input.ConnectionScheme,
                input.DebugApiPort,
                input.GatewayApiPort,
                input.Hostname);
            await beehiveDbContext.BeeNodes.CreateAsync(node);

            logger.NodeRegistered(
                node.Id,
                node.BaseUrl,
                node.GatewayPort,
                node.DebugPort);

            return new BeeNodeDto(node);
        }

        public async Task DeletePinAsync(string id, string hash)
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

        public async Task<PinnedResourceDto> GetPinDetailsAsync(string id, string hash)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);

            if (await beeNodeInstance.IsPinningResourceAsync(hash))
                return new PinnedResourceDto(hash, id, PinnedResourceStatusDto.Pinned);
            else if (beeNodeInstance.InProgressPins.Contains(hash))
                return new PinnedResourceDto(hash, id, PinnedResourceStatusDto.InProgress);
            else
                return new PinnedResourceDto(hash, id, PinnedResourceStatusDto.NotPinned);
        }

        public async Task<IEnumerable<string>> GetPinsByNodeAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            var readyPins = await beeNodeInstance.Client.GatewayClient!.GetAllPinsAsync();
            var inProgressPins = beeNodeInstance.InProgressPins;
            return readyPins.Union(inProgressPins);
        }

        public async Task<PostageBatchDto> GetPostageBatchDetailsAsync(string id, string batchId)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            try
            {
                var postageBatch = await beeNodeInstance.Client.DebugClient!.GetPostageBatchAsync(batchId);
                return new PostageBatchDto(postageBatch);
            }
            catch (BeeNetDebugApiException ex) when (ex.StatusCode == 400)
            {
                throw new KeyNotFoundException();
            }
        }

        public async Task<IEnumerable<PostageBatchDto>> GetPostageBatchesByNodeAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            var batches = await beeNodeInstance.Client.DebugClient!.GetOwnedPostageBatchesByNodeAsync();
            return batches.Select(b => new PostageBatchDto(b));
        }

        public async Task NotifyPinningOfUploadedContentAsync(string id, string hash)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            beeNodeInstance.NotifyPinnedResource(hash);
        }

        public async Task RemoveBeeNodeAsync(string id)
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            await beehiveDbContext.BeeNodes.DeleteAsync(id);

            logger.NodeRemoved(id);
        }
    }
}

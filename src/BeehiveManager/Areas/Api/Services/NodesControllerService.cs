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
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core.Extensions;
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
        private readonly IBeeNodeLiveManager beeNodeLiveManager;
        private readonly IBeehiveDbContext context;

        // Constructor.
        public NodesControllerService(
            IBeeNodeLiveManager beeNodeLiveManager,
            IBeehiveDbContext context)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
            this.context = context;
        }

        // Methods.
        public async Task<BeeNodeDto> AddBeeNodeAsync(BeeNodeInput input)
        {
            if (input is null)
                throw new ArgumentNullException(nameof(input));

            // Create node.
            var node = new BeeNode(
                input.DebugApiPort,
                input.GatewayApiPort,
                input.Url);
            await context.BeeNodes.CreateAsync(node);

            return new BeeNodeDto(node);
        }

        public async Task<BeeNodeDto> FindByIdAsync(string id) =>
            new BeeNodeDto(await context.BeeNodes.FindOneAsync(id));

        public async Task<PostageBatchDto> FindPostageBatchOnNodeAsync(string id, string batchId)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            var postageBatch = await beeNodeInstance.Client.DebugClient!.GetPostageBatchAsync(batchId);
            return new PostageBatchDto(postageBatch);
        }

        public async Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take) =>
            (await context.BeeNodes.QueryElementsAsync(elements =>
                elements.PaginateDescending(n => n.CreationDateTime, page, take)
                        .ToListAsync()))
            .Select(n => new BeeNodeDto(n));

        public async Task<IEnumerable<PostageBatchDto>> GetOwnedPostageBatchesByNodeAsync(string id)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(id);
            var batches = await beeNodeInstance.Client.DebugClient!.GetOwnedPostageBatchesByNodeAsync();
            return batches.Select(b => new PostageBatchDto(b));
        }

        public async Task RemoveBeeNodeAsync(string id)
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            await context.BeeNodes.DeleteAsync(id);
        }
    }
}

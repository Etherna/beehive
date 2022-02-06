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
using Etherna.BeehiveManager.Services.Tasks;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core.Extensions;
using Hangfire;
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
        private readonly IBackgroundJobClient backgroundJobClient;
        private readonly IBeeNodeClientsManager beeNodesManager;
        private readonly IBeehiveDbContext context;

        // Constructor.
        public NodesControllerService(
            IBackgroundJobClient backgroundJobClient,
            IBeeNodeClientsManager beeNodesManager,
            IBeehiveDbContext context)
        {
            this.backgroundJobClient = backgroundJobClient;
            this.beeNodesManager = beeNodesManager;
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
                input.GatwayApiPort,
                input.Url);
            await context.BeeNodes.CreateAsync(node);

            // Try immediatly to retrive node info from instance.
            EnqueueRetrieveNodeAddresses(node.Id);

            return new BeeNodeDto(node);
        }

        public void EnqueueRetrieveNodeAddresses(string id) =>
            backgroundJobClient.Enqueue<IRetrieveNodeAddressesTask>(task => task.RunAsync(id));

        public async Task<BeeNodeDto> FindByIdAsync(string id) =>
            new BeeNodeDto(await context.BeeNodes.FindOneAsync(id));

        public async Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take) =>
            (await context.BeeNodes.QueryElementsAsync(elements =>
                elements.PaginateDescending(n => n.CreationDateTime, page, take)
                        .ToListAsync()))
            .Select(n => new BeeNodeDto(n));

        public async Task RemoveBeeNodeAsync(string id)
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            beeNodesManager.RemoveBeeNodeClient(id);
            await context.BeeNodes.DeleteAsync(id);
        }
    }
}

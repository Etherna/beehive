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
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Tasks;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using Hangfire;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class PinningControllerService : IPinningControllerService
    {
        // Fields.
        private readonly IBackgroundJobClient backgroundJobClient;
        private readonly IBeeNodeLiveManager beeNodeLiveManager;
        private readonly IBeehiveDbContext dbContext;

        // Constructor.
        public PinningControllerService(
            IBackgroundJobClient backgroundJobClient,
            IBeeNodeLiveManager beeNodeLiveManager,
            IBeehiveDbContext dbContext)
        {
            this.backgroundJobClient = backgroundJobClient;
            this.beeNodeLiveManager = beeNodeLiveManager;
            this.dbContext = dbContext;
        }

        // Methods.
        public async Task<IEnumerable<BeeNodeDto>> FindBeeNodesPinningContentAsync(string hash, bool requireAliveNodes)
        {
            var beeNodeLiveInstances = beeNodeLiveManager.GetBeeNodeLiveInstancesByPinnedContent(hash, requireAliveNodes);
            var beeNodes = new List<BeeNode>();
            foreach (var instance in beeNodeLiveInstances)
                beeNodes.Add(await dbContext.BeeNodes.FindOneAsync(instance.Id));

            return beeNodes.Select(n => new BeeNodeDto(n));
        }

        public async Task<string> PinContentInNodeAsync(string hash, string? nodeId)
        {
            // Try to select an healthy node that doesn't already own the pin, if not specified.
            nodeId ??= (await beeNodeLiveManager.TrySelectHealthyNodeAsync(
                BeeNodeSelectionMode.RoundRobin,
                "pinNewContent",
                async node => !await node.IsPinningResourceAsync(hash)))?.Id;

            if (nodeId is null)
                throw new InvalidOperationException("No healthy nodes available to pin");

            // Schedule task.
            backgroundJobClient.Enqueue<IPinContentInNodeTask>(
                task => task.RunAsync(hash, nodeId));

            return nodeId;
        }
    }
}

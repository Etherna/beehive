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
using Etherna.BeehiveManager.Services.Utilities;
using MoreLinq;
using Nethereum.Util;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class LoadBalancerControllerService : ILoadBalancerControllerService
    {
        // Fields.
        private readonly IBeeNodeLiveManager beeNodeLiveManager;
        private readonly IBeehiveDbContext dbContext;

        // Constructor.
        public LoadBalancerControllerService(
            IBeeNodeLiveManager beeNodeLiveManager,
            IBeehiveDbContext dbContext)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
            this.dbContext = dbContext;
        }

        // Methods.
        public async Task<BeeNodeDto> SelectDownloadNodeAsync(string hash)
        {
            // Try to find a pinning node.
            var beeNodeLiveInstances = beeNodeLiveManager.GetBeeNodeLiveInstancesByPinnedContent(hash, true);
            if (beeNodeLiveInstances.Any())
            {
                //select a random one
                var instance = beeNodeLiveInstances.RandomSubset(1).First();
                var node = await dbContext.BeeNodes.FindOneAsync(instance.Id);
                return new BeeNodeDto(node);
            }

            // If there isn't any pinning node, select an alive node.
            beeNodeLiveInstances = beeNodeLiveManager.HealthyNodes;
            if (beeNodeLiveInstances.Any())
            {
                //select a random one
                var instance = beeNodeLiveInstances.RandomSubset(1).First();
                var node = await dbContext.BeeNodes.FindOneAsync(instance.Id);
                return new BeeNodeDto(node);
            }

            // Throw exception because doesn't exist any available node.
            throw new InvalidOperationException("Can't select a valid node");
        }

        public async Task<BeeNodeDto> SelectSocNodeAsync(string address)
        {
            address = address.ConvertToEthereumChecksumAddress();

            // Try to find ether address configuration.
            var etherAddress = await dbContext.EtherAddresses.TryFindOneAsync(a => a.Address == address);
            BeeNode selectedNode;

            // If configuration doesn't exist, create it.
            if (etherAddress is null)
            {
                //select a random healthy node
                selectedNode = await SelectRandomHealthyNodeAsync();

                //create configuration with selected node as preferred
                etherAddress = new EtherAddress(address) { PreferredSocNode = selectedNode };
                await dbContext.EtherAddresses.CreateAsync(etherAddress);
            }

            // Else, if there is no preferred soc node, select one random and update config.
            else if (etherAddress.PreferredSocNode is null)
            {
                //select a random healthy node
                selectedNode = await SelectRandomHealthyNodeAsync();

                //update configuration with selected node as preferred
                etherAddress.PreferredSocNode = selectedNode;
                await dbContext.SaveChangesAsync();
            }

            // Verify if preferred soc node is healthy, or select another random.
            else
            {
                selectedNode = beeNodeLiveManager.HealthyNodes.Any(n => n.Id == etherAddress.PreferredSocNode.Id) ?
                    etherAddress.PreferredSocNode :
                    await SelectRandomHealthyNodeAsync();
            }

            return new BeeNodeDto(selectedNode);
        }

        // Helpers.
        private async Task<BeeNode> SelectRandomHealthyNodeAsync()
        {
            var beeNodeLiveInstances = beeNodeLiveManager.HealthyNodes;
            if (!beeNodeLiveInstances.Any())
                throw new InvalidOperationException("Can't select a valid healthy node");

            var instance = beeNodeLiveInstances.RandomSubset(1).First();
            return await dbContext.BeeNodes.FindOneAsync(instance.Id);
        }
    }
}

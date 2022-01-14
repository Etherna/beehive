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

using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.DtoModel;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class RetrieveNodeAddressesTask : IRetrieveNodeAddressesTask
    {
        // Fields.
        private readonly IBeeNodesManager beeNodesManager;
        private readonly IBeehiveContext context;

        // Constructors.
        public RetrieveNodeAddressesTask(
            IBeeNodesManager beeNodesManager,
            IBeehiveContext context)
        {
            this.beeNodesManager = beeNodesManager;
            this.context = context;
        }

        // Methods.
        public async Task RunAsync(string nodeId)
        {
            // Get client.
            var node = await context.BeeNodes.TryFindOneAsync(nodeId);

            // Verify conditions.
            if (node is null)
                return; //can't find the node in db
            if (node.Addresses is not null)
                return; //don't need any operation
            if (node.DebugPort is null)
                return; //node is not configured for use debug api

            // Get info.
            var nodeClient = beeNodesManager.GetBeeNodeClient(node);
            AddressDetailDto response;
            try { response = await nodeClient.DebugClient!.AddressesAsync(); }
            catch (BeeNetDebugApiException) { return; } //issues contacting the node instance api
            catch (HttpRequestException) { return; }

            // Update node.
            node.SetAddresses(new BeeNodeAddresses(
                response.Ethereum,
                response.Overlay,
                response.PssPublicKey,
                response.PublicKey));

            // Save changes.
            await context.SaveChangesAsync();
        }
    }
}

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

using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeeNet;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Services.Utilities
{
    class BeeNodesManager : IBeeNodesManager
    {
        // Fields.
        private readonly Dictionary<string, BeeNodeClient> _nodeClients = new();

        // Properties.
        public IReadOnlyDictionary<string, BeeNodeClient> NodeClients => _nodeClients;

        // Methods.
        public BeeNodeClient GetBeeNodeClient(BeeNode beeNode)
        {
            if (_nodeClients.ContainsKey(beeNode.Id))
                return _nodeClients[beeNode.Id];

            var client = new BeeNodeClient(beeNode.Url.AbsoluteUri, beeNode.GatewayPort, beeNode.DebugPort);
            _nodeClients.Add(beeNode.Id, client);

            return client;
        }

        public bool RemoveBeeNodeClient(string id) =>
            _nodeClients.Remove(id);
    }
}

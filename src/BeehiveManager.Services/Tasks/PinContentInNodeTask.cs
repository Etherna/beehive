// Copyright 2021-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Etherna.BeehiveManager.Services.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Tasks
{
    public class PinContentInNodeTask : IPinContentInNodeTask
    {
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        public PinContentInNodeTask(
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        public async Task RunAsync(string contentHash, string nodeId)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);

            // Try to pin.
            try { await beeNodeInstance.PinResourceAsync(contentHash); }
            catch (KeyNotFoundException) { }
        }
    }
}

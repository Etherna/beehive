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

using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Services.Utilities;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class ChainControllerService : IChainControllerService
    {
        // Fields.
        private readonly IBeeNodeLiveManager liveManager;

        // Constructor.
        public ChainControllerService(
            IBeeNodeLiveManager liveManager)
        {
            this.liveManager = liveManager;
        }

        // Methods.
        public ChainStateDto? GetChainState() =>
            liveManager.ChainState is null ? null :
            new ChainStateDto(liveManager.ChainState);
    }
}

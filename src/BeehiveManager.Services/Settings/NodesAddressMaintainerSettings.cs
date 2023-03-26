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

namespace Etherna.BeehiveManager.Services.Settings
{
    public class NodesAddressMaintainerSettings
    {
        // Consts.
        public const string ConfigPosition = "FundNodes";

        // Properties.
        public string? BzzContractAddress { get; set; }
        public decimal? BzzMinTrigger { get; set; }
        public decimal? BzzTargetAmount { get; set; }
        public long? ChainId { get; set; }
        public string? ChestPrivateKey { get; set; }
        public string? RPCEndpoint { get; set; }
        public bool RunBzzFunding => BzzContractAddress is not null &&
            ChainId.HasValue &&
            ChestPrivateKey is not null &&
            (RPCEndpoint is not null || WebsocketEndpoint is not null) &&
            BzzMinTrigger.HasValue &&
            BzzTargetAmount > BzzMinTrigger;
        public bool RunXDaiFunding => ChainId.HasValue &&
            ChestPrivateKey is not null &&
            (RPCEndpoint is not null || WebsocketEndpoint is not null) &&
            XDaiMinTrigger.HasValue &&
            XDaiTargetAmount > XDaiMinTrigger;
        public decimal? XDaiMinTrigger { get; set; }
        public decimal? XDaiTargetAmount { get; set; }
        public string? WebsocketEndpoint { get; set; }
    }
}

// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

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

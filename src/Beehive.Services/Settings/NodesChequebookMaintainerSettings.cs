// Copyright 2021-present Etherna SA
// This file is part of Beehive.
// 
// Beehive is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Beehive is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Beehive.
// If not, see <https://www.gnu.org/licenses/>.

namespace Etherna.Beehive.Services.Settings
{
    public class NodesChequebookMaintainerSettings
    {
        // Consts.
        public const string ConfigPosition = "ChequebookLimits";

        // Properties.
        public decimal? BzzMaxTrigger { get; set; }
        public decimal? BzzMinTrigger { get; set; }
        public decimal? BzzTargetAmount { get; set; }
        public bool RunDeposits => BzzTargetAmount.HasValue && BzzMinTrigger.HasValue;
        public bool RunWithdraws => BzzTargetAmount.HasValue && BzzMaxTrigger.HasValue;
    }
}

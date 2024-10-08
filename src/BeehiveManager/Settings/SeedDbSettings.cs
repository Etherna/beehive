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

using System;

namespace Etherna.BeehiveManager.Settings
{
#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA1819 // Properties should not return arrays
    public class SeedDbSettings
    {
        // Internal classes.
        public class BeeNode
        {
            public bool EnableBatchCreation { get; set; } = true;
            public int GatewayPort { get; set; } = 1633;
            public string Hostname { get; set; } = "localhost";
            public string Scheme { get; set; } = "http";
        }

        // Consts.
        public const string ConfigPosition = "SeedDb";

        // Properties.
        public BeeNode[] BeeNodes { get; set; } = Array.Empty<BeeNode>();
    }
#pragma warning restore CA1819 // Properties should not return arrays
#pragma warning restore CA1034 // Nested types should not be visible
}

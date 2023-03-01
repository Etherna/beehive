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
            public int DebugPort { get; set; } = 1635;
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

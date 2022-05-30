using System;

namespace Etherna.BeehiveManager.Configs
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

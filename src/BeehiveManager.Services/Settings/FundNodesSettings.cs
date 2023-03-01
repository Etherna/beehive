namespace Etherna.BeehiveManager.Services.Settings
{
    public class FundNodesSettings
    {
        // Consts.
        public const string ConfigPosition = "FundNodes";

        // Properties.
        public long? ChainId { get; set; }
        public string? ChestPrivateKey { get; set; }
        public int? BzzMinTrigger { get; set; }
        public int? BzzTargetAmount { get; set; }
        public string? RPCEndpoint { get; set; }
        public bool RunBzzFunding => ChainId.HasValue &&
            ChestPrivateKey is not null &&
            RPCEndpoint is not null &&
            BzzMinTrigger.HasValue &&
            BzzTargetAmount > BzzMinTrigger;
        public bool RunXdaiFunding => ChainId.HasValue &&
            ChestPrivateKey is not null &&
            RPCEndpoint is not null &&
            XDaiMinTrigger.HasValue &&
            XDaiTargetAmount > XDaiMinTrigger;
        public int? XDaiMinTrigger { get; set; }
        public int? XDaiTargetAmount { get; set; }
    }
}

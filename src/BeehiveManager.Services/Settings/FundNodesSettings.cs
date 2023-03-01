namespace Etherna.BeehiveManager.Services.Settings
{
    public class FundNodesSettings
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

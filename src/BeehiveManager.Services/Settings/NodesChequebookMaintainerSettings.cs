namespace Etherna.BeehiveManager.Services.Settings
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

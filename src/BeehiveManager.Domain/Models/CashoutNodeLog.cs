namespace Etherna.BeehiveManager.Domain.Models
{
    public class CashoutNodeLog : NodeLogBase
    {
        // Constructors.
        public CashoutNodeLog(
            BeeNode beeNode,
            long totalCashout)
            : base(beeNode)
        {
            TotalCashout = totalCashout;
        }

        // Properties.
        public virtual long TotalCashout { get; protected set; }
    }
}

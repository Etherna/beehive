using System.Collections.Generic;

namespace Etherna.BeehiveManager.Domain.Models
{
    public class CashoutNodeLog : NodeLogBase
    {
        // Constructors.
        public CashoutNodeLog(
            BeeNode beeNode,
            IEnumerable<string> txs,
            long totalCashout)
            : base(beeNode)
        {
            Txs = txs;
            TotalCashout = totalCashout;
        }
        protected CashoutNodeLog() { }


        // Properties.
        public virtual long TotalCashout { get; protected set; }
        public virtual IEnumerable<string> Txs { get; protected set; } = default!;
    }
}

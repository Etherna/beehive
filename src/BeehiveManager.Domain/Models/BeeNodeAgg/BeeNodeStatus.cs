using System;

namespace Etherna.BeehiveManager.Domain.Models.BeeNodeAgg
{
    public class BeeNodeStatus : ModelBase
    {
        // Constructors.
        public BeeNodeStatus(
            long uncashedTotal)
        {
            ReadTime = DateTime.Now;
            UncashedTotal = uncashedTotal;
        }
        protected BeeNodeStatus() { }

        // Properties.
        public virtual DateTime ReadTime { get; protected set; }
        public virtual long UncashedTotal { get; protected set; }
    }
}

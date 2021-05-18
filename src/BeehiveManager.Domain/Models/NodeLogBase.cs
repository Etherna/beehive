using System;

namespace Etherna.BeehiveManager.Domain.Models
{
    public abstract class NodeLogBase : EntityModelBase<string>
    {
        // Constructors.
        protected NodeLogBase(BeeNode node)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
        }
        protected NodeLogBase() { }

        // Properties.
        public virtual BeeNode Node { get; protected set; } = default!;
    }
}

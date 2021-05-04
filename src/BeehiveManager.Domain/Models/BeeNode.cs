namespace Etherna.BeehiveManager.Domain.Models
{
    public class BeeNode : EntityModelBase<string>
    {
        // Constructors.
        protected BeeNode() { }

        // Properties.
        public virtual string? EthAddress { get; protected set; }
    }
}

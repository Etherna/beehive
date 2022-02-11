using Etherna.BeehiveManager.Domain.Models;
using Etherna.DomainEvents;

namespace Etherna.BeehiveManager.Domain.Events
{
    public class SetBeeNodeAddressesEvent : IDomainEvent
    {
        public SetBeeNodeAddressesEvent(BeeNode node)
        {
            Node = node;
        }

        public BeeNode Node { get; }
    }
}

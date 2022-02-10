using Etherna.BeehiveManager.Domain.Events;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.DomainEvents;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.EventHandlers
{
    class OnSetBeeNodeAddressesThenUpdateNodeStatusHandler : EventHandlerBase<SetBeeNodeAddressesEvent>
    {
        // Fields.
        private readonly IBeeNodesStatusManager nodeStatusManager;

        // Constructor.
        public OnSetBeeNodeAddressesThenUpdateNodeStatusHandler(
            IBeeNodesStatusManager nodeStatusManager)
        {
            this.nodeStatusManager = nodeStatusManager;
        }

        // Methods.
        public override Task HandleAsync(SetBeeNodeAddressesEvent @event)
        {
            nodeStatusManager.UpdateNodeInfo(@event.Node);
            return Task.CompletedTask;
        }
    }
}

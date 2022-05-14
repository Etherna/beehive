using Etherna.BeehiveManager.Domain.Events;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.DomainEvents;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.EventHandlers
{
    class OnSetBeeNodeAddressesThenUpdateNodeStatusHandler : EventHandlerBase<SetBeeNodeAddressesEvent>
    {
        // Fields.
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        // Constructor.
        public OnSetBeeNodeAddressesThenUpdateNodeStatusHandler(
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        // Methods.
        public override Task HandleAsync(SetBeeNodeAddressesEvent @event)
        {
            beeNodeLiveManager.UpdateNodeInfo(@event.Node);
            return Task.CompletedTask;
        }
    }
}

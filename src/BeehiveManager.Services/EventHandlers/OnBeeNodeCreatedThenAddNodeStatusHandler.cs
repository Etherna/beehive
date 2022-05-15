using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.DomainEvents;
using Etherna.DomainEvents.Events;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.EventHandlers
{
    class OnBeeNodeCreatedThenAddNodeStatusHandler : EventHandlerBase<EntityCreatedEvent<BeeNode>>
    {
        // Fields.
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        // Constructor.
        public OnBeeNodeCreatedThenAddNodeStatusHandler(
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        // Methods.
        public override async Task HandleAsync(EntityCreatedEvent<BeeNode> @event)
        {
            await beeNodeLiveManager.AddBeeNodeAsync(@event.Entity);
        }
    }
}

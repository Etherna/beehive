using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.DomainEvents;
using Etherna.DomainEvents.Events;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.EventHandlers
{
    class OnBeeNodeDeletedThenRemoveNodeStatusHandler : EventHandlerBase<EntityDeletedEvent<BeeNode>>
    {
        // Fields.
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        // Constructor.
        public OnBeeNodeDeletedThenRemoveNodeStatusHandler(
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        // Methods.
        public override Task HandleAsync(EntityDeletedEvent<BeeNode> @event)
        {
            beeNodeLiveManager.RemoveBeeNode(@event.Entity.Id);
            return Task.CompletedTask;
        }
    }
}

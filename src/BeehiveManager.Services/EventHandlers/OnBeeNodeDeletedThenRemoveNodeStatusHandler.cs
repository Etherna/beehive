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
        private readonly IBeeNodesStatusManager nodeStatusManager;

        // Constructor.
        public OnBeeNodeDeletedThenRemoveNodeStatusHandler(
            IBeeNodesStatusManager nodeStatusManager)
        {
            this.nodeStatusManager = nodeStatusManager;
        }

        // Methods.
        public override Task HandleAsync(EntityDeletedEvent<BeeNode> @event)
        {
            nodeStatusManager.RemoveBeeNode(@event.Entity.Id);
            return Task.CompletedTask;
        }
    }
}

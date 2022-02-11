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
        private readonly IBeeNodesStatusManager nodeStatusManager;

        // Constructor.
        public OnBeeNodeCreatedThenAddNodeStatusHandler(
            IBeeNodesStatusManager nodeStatusManager)
        {
            this.nodeStatusManager = nodeStatusManager;
        }

        // Methods.
        public override Task HandleAsync(EntityCreatedEvent<BeeNode> @event)
        {
            nodeStatusManager.AddBeeNode(@event.Entity);
            return Task.CompletedTask;
        }
    }
}

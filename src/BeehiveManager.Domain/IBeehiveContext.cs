using Etherna.BeehiveManager.Domain.Models;
using Etherna.DomainEvents;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Repositories;

namespace Etherna.BeehiveManager.Domain
{
    public interface IBeehiveContext : IDbContext
    {
        ICollectionRepository<BeeNode, string> BeeNodes { get; }

        IEventDispatcher EventDispatcher { get; }
    }
}

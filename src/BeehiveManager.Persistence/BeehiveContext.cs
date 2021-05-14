using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Persistence.Repositories;
using Etherna.DomainEvents;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Options;
using Etherna.MongODM.Core.Repositories;
using Etherna.MongODM.Core.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Persistence
{
    public class BeehiveContext : DbContext, IBeehiveContext, IEventDispatcherDbContext
    {
        // Consts.
        private const string SerializersNamespace = "Etherna.BeehiveManager.Persistence.ModelMaps";

        // Constructor.
        public BeehiveContext(
            IDbDependencies dbDependencies,
            IEventDispatcher eventDispatcher,
            DbContextOptions<BeehiveContext> options)
            : base(dbDependencies, options)
        {
            EventDispatcher = eventDispatcher;
        }

        // Properties.
        //repositories
        public ICollectionRepository<BeeNode, string> BeeNodes { get; } = new DomainCollectionRepository<BeeNode, string>(
            new CollectionRepositoryOptions<BeeNode>("beeNodes")
            {
                IndexBuilders = new[]
                {
                    (Builders<BeeNode>.IndexKeys.Ascending(n => n.Addresses.Ethereum), new CreateIndexOptions<BeeNode> { Sparse = true, Unique = true }),
                }
            });

        //other properties
        public IEventDispatcher EventDispatcher { get; }

        // Protected properties.
        protected override IEnumerable<IModelMapsCollector> ModelMapsCollectors =>
            from t in typeof(BeehiveContext).GetTypeInfo().Assembly.GetTypes()
            where t.IsClass && t.Namespace == SerializersNamespace
            where t.GetInterfaces().Contains(typeof(IModelMapsCollector))
            select Activator.CreateInstance(t) as IModelMapsCollector;

        // Methods.
        public override Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Dispatch events.
            foreach (var model in ChangedModelsList.Where(m => m is EntityModelBase)
                                                   .Select(m => (EntityModelBase)m))
            {
                EventDispatcher.DispatchAsync(model.Events);
                model.ClearEvents();
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}

//   Copyright 2021-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Persistence.Repositories;
using Etherna.DomainEvents;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Repositories;
using Etherna.MongODM.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Persistence
{
    public class BeehiveDbContext : DbContext, IBeehiveDbContext, IEventDispatcherDbContext
    {
        // Consts.
        private const string SerializersNamespace = "Etherna.BeehiveManager.Persistence.ModelMaps";

        // Fields.
        private readonly IEnumerable<BeeNode>? seedDbBeeNodes;

        // Constructor.
        public BeehiveDbContext(
            IEventDispatcher eventDispatcher,
            IEnumerable<BeeNode>? seedDbBeeNodes)
        {
            EventDispatcher = eventDispatcher;
            this.seedDbBeeNodes = seedDbBeeNodes;
        }

        // Properties.
        //repositories
        public ICollectionRepository<BeeNode, string> BeeNodes { get; } = new DomainCollectionRepository<BeeNode, string>(
            new CollectionRepositoryOptions<BeeNode>("beeNodes")
            {
                IndexBuilders = new[]
                {
                    (Builders<BeeNode>.IndexKeys.Ascending(n => n.DebugPort)
                                                .Ascending(n => n.Hostname), new CreateIndexOptions<BeeNode> { Unique = true }),
                    (Builders<BeeNode>.IndexKeys.Ascending(n => n.GatewayPort)
                                                .Ascending(n => n.Hostname), new CreateIndexOptions<BeeNode> { Unique = true })
                }
            });
        public ICollectionRepository<EtherAddressConfig, string> EtherAddressConfigs { get; } = new DomainCollectionRepository<EtherAddressConfig, string>(
            new CollectionRepositoryOptions<EtherAddressConfig>("etherAddressConfigs")
            {
                IndexBuilders = new[]
                {
                    (Builders<EtherAddressConfig>.IndexKeys.Ascending(a => a.Address), new CreateIndexOptions<EtherAddressConfig> { Unique = true }),
                }
            });
        public ICollectionRepository<NodeLogBase, string> NodeLogs { get; } = new DomainCollectionRepository<NodeLogBase, string>("nodeLogs");

        //other properties
        public IEventDispatcher EventDispatcher { get; }

        // Protected properties.
        protected override IEnumerable<IModelMapsCollector> ModelMapsCollectors =>
            from t in typeof(BeehiveDbContext).GetTypeInfo().Assembly.GetTypes()
            where t.IsClass && t.Namespace == SerializersNamespace
            where t.GetInterfaces().Contains(typeof(IModelMapsCollector))
            select Activator.CreateInstance(t) as IModelMapsCollector;

        // Public methods.
        public override async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Dispatch events.
            foreach (var model in ChangedModelsList.Where(m => m is EntityModelBase)
                                                   .Select(m => (EntityModelBase)m))
            {
                await EventDispatcher.DispatchAsync(model.Events);
                model.ClearEvents();
            }

            await base.SaveChangesAsync(cancellationToken);
        }

        // Protected methods.
        protected override async Task SeedAsync()
        {
            if (seedDbBeeNodes is null)
                return;

            foreach (var node in seedDbBeeNodes)
                await BeeNodes.CreateAsync(node);
        }
    }
}

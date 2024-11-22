// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

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
    public class BeehiveManagerDbContext(
        IEventDispatcher eventDispatcher,
        IEnumerable<BeeNode>? seedDbBeeNodes)
        : DbContext, IBeehiveDbContext, IEventDispatcherDbContext
    {
        // Consts.
        private const string SerializersNamespace = "Etherna.BeehiveManager.Persistence.ModelMaps";

        // Properties.
        //repositories
        public IRepository<BeeNode, string> BeeNodes { get; } = new DomainRepository<BeeNode, string>(
            new RepositoryOptions<BeeNode>("beeNodes")
            {
                IndexBuilders =
                [
                    (Builders<BeeNode>.IndexKeys.Ascending(n => n.GatewayPort)
                                                .Ascending(n => n.Hostname), new CreateIndexOptions<BeeNode> { Unique = true })
                ]
            });

        //other properties
        public IEventDispatcher EventDispatcher { get; } = eventDispatcher;

        // Protected properties.
        protected override IEnumerable<IModelMapsCollector> ModelMapsCollectors =>
            from t in typeof(BeehiveManagerDbContext).GetTypeInfo().Assembly.GetTypes()
            where t.IsClass && t.Namespace == SerializersNamespace
            where t.GetInterfaces().Contains(typeof(IModelMapsCollector))
            select Activator.CreateInstance(t) as IModelMapsCollector;

        // Public methods.
        public override async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // Dispatch events.
            foreach (var model in ChangedModelsList.OfType<EntityModelBase>())
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

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

using Etherna.BeehiveManager.Domain.Models;
using Etherna.DomainEvents;
using Etherna.DomainEvents.Events;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Persistence.Repositories
{
    public class DomainCollectionRepository<TModel, TKey> :
        CollectionRepository<TModel, TKey>
        where TModel : EntityModelBase<TKey>
    {
        // Constructors and initialization.
        public DomainCollectionRepository(string name)
            : base(name)
        { }

        public DomainCollectionRepository(CollectionRepositoryOptions<TModel> options)
            : base(options)
        { }

        public override void Initialize(IDbContext dbContext)
        {
            if (dbContext is not IEventDispatcherDbContext)
                throw new InvalidOperationException($"DbContext needs to implement {nameof(IEventDispatcherDbContext)}");

            base.Initialize(dbContext);
        }

        // Properties.
        public IEventDispatcher EventDispatcher => ((IEventDispatcherDbContext)DbContext).EventDispatcher;

        // Methods.
        public override async Task CreateAsync(IEnumerable<TModel> models, CancellationToken cancellationToken = default)
        {
            await base.CreateAsync(models, cancellationToken);

            // Dispatch created events.
            if (EventDispatcher != null) //could be null in seeding
                await EventDispatcher.DispatchAsync(
                    models.Select(m => new EntityCreatedEvent<TModel>(m)));
        }

        public override async Task CreateAsync(TModel model, CancellationToken cancellationToken = default)
        {
            await base.CreateAsync(model, cancellationToken);

            // Dispatch created event.
            if (EventDispatcher != null) //could be null in seeding
                await EventDispatcher.DispatchAsync(
                    new EntityCreatedEvent<TModel>(model));
        }

        public override async Task DeleteAsync(TModel model, CancellationToken cancellationToken = default)
        {
            await base.DeleteAsync(model, cancellationToken);

            // Dispatch deleted event.
            if (EventDispatcher != null) //could be null in seeding
                await EventDispatcher.DispatchAsync(
                    new EntityDeletedEvent<TModel>(model));
        }
    }
}

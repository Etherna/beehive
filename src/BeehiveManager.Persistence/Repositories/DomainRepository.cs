// Copyright 2021-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
    public class DomainRepository<TModel, TKey> :
        Repository<TModel, TKey>
        where TModel : EntityModelBase<TKey>
    {
        // Constructors and initialization.
        public DomainRepository(string name)
            : base(name)
        { }

        public DomainRepository(RepositoryOptions<TModel> options)
            : base(options)
        { }

        // Properties.
        public IEventDispatcher? EventDispatcher => (DbContext as IEventDispatcherDbContext)?.EventDispatcher;

        // Methods.
        public override async Task CreateAsync(IEnumerable<TModel> models, CancellationToken cancellationToken = default)
        {
            if (models is null)
                throw new ArgumentNullException(nameof(models));

            // Create entity.
            await base.CreateAsync(models, cancellationToken);

            // Dispatch events.
            if (EventDispatcher != null)
            {
                //created event
                await EventDispatcher.DispatchAsync(models.Select(m => new EntityCreatedEvent<TModel>(m)));

                //custom events
                foreach (var model in models)
                {
                    await EventDispatcher.DispatchAsync(model.Events);
                    model.ClearEvents();
                }
            }
        }

        public override async Task CreateAsync(TModel model, CancellationToken cancellationToken = default)
        {
            if (model is null)
                throw new ArgumentNullException(nameof(model));

            // Create entity.
            await base.CreateAsync(model, cancellationToken);

            // Dispatch events.
            if (EventDispatcher != null)
            {
                //created event
                await EventDispatcher.DispatchAsync(new EntityCreatedEvent<TModel>(model));

                //custom events
                await EventDispatcher.DispatchAsync(model.Events);
                model.ClearEvents();
            }
        }

        public override async Task DeleteAsync(TModel model, CancellationToken cancellationToken = default)
        {
            if (model is null)
                throw new ArgumentNullException(nameof(model));

            // Dispatch custom events.
            if (EventDispatcher != null)
            {
                await EventDispatcher.DispatchAsync(model.Events);
                model.ClearEvents();
            }

            // Delete entity.
            await base.DeleteAsync(model, cancellationToken);

            // Dispatch deleted event.
            if (EventDispatcher != null)
                await EventDispatcher.DispatchAsync(
                    new EntityDeletedEvent<TModel>(model));
        }
    }
}

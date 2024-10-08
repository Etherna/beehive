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
            ArgumentNullException.ThrowIfNull(models, nameof(models));

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
            ArgumentNullException.ThrowIfNull(model, nameof(model));

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
            ArgumentNullException.ThrowIfNull(model, nameof(model));

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

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
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.DomainEvents;
using Etherna.DomainEvents.Events;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.EventHandlers
{
    internal sealed class OnBeeNodeCreatedThenAddNodeStatusHandler(IBeeNodeLiveManager beeNodeLiveManager)
        : EventHandlerBase<EntityCreatedEvent<BeeNode>>
    {
        // Methods.
        public override async Task HandleAsync(EntityCreatedEvent<BeeNode> @event)
        {
            await beeNodeLiveManager.AddBeeNodeAsync(@event.Entity);
        }
    }
}

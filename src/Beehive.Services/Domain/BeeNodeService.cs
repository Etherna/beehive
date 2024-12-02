// Copyright 2021-present Etherna SA
// This file is part of Beehive.
// 
// Beehive is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Beehive is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Beehive.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using System;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public class BeeNodeService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext)
        : IBeeNodeService
    {
        // Methods.
        public async Task<BeeNode> SelectRandomHealthyNodeAsync()
        {
            var instance = await beeNodeLiveManager.TrySelectHealthyNodeAsync(BeeNodeSelectionMode.Random) ??
                throw new InvalidOperationException("Can't select a valid healthy node");
            return await dbContext.BeeNodes.FindOneAsync(instance.Id);
        }
    }
}

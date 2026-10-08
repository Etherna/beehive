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

using Etherna.Beehive.Domain.Models;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Serialization;

namespace Etherna.Beehive.Persistence.ModelMaps
{
    internal sealed class LocksMap : IModelMapsCollector
    {
        public void Register(IDbContextEngine dbContextEngine)
        {
            dbContextEngine.MapRegistry.AddModelMap<ChunkPinLock>( //v0.4.0
                "a73d46c1-b548-4461-b4d3-b947de2f97e9");

            dbContextEngine.MapRegistry.AddModelMap<ChunkPushLock>( //0.4.7
                "c5eb888f-e4eb-44ca-9513-e69a52d43f24");

            dbContextEngine.MapRegistry.AddModelMap<PostageBatchLock>( //v0.4.0
                "e26fdf55-0245-4ead-b20a-13296e69d61d");

            dbContextEngine.MapRegistry.AddModelMap<ResourceLockBase>( //v0.4.0
                "1d1f7db3-3cee-4845-8888-b822f6a7f471",
                mm =>
                {
                    mm.AutoMap();

                    // Ignore default values.
                    mm.GetMemberMap(n => n.Counter).SetIgnoreIfDefault(true);
                });
        }
    }
}
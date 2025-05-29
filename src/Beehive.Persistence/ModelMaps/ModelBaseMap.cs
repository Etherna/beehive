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
using Etherna.MongoDB.Bson;
using Etherna.MongoDB.Bson.Serialization.IdGenerators;
using Etherna.MongoDB.Bson.Serialization.Serializers;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;

namespace Etherna.Beehive.Persistence.ModelMaps
{
    internal sealed class ModelBaseMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.MapRegistry.AddModelMap<ModelBase>("7653dfab-f715-42d1-8d3d-bbca69755399");
            dbContext.MapRegistry.AddModelMap<EntityModelBase>("5cddcc0c-1a61-443c-bb72-98d1344cafb4");
            dbContext.MapRegistry.AddModelMap<EntityModelBase<string>>("3d7b0f5d-d490-495e-af05-6114e8f8d2f4", modelMap =>
            {
                modelMap.AutoMap();

                // Set Id representation.
                modelMap.IdMemberMap.SetSerializer(new StringSerializer(BsonType.ObjectId))
                                    .SetIdGenerator(new StringObjectIdGenerator());
            });
        }
    }
}

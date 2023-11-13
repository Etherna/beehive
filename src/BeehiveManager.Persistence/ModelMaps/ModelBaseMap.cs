//   Copyright 2021-present Etherna SA
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
using Etherna.MongoDB.Bson;
using Etherna.MongoDB.Bson.Serialization.IdGenerators;
using Etherna.MongoDB.Bson.Serialization.Serializers;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;

namespace Etherna.BeehiveManager.Persistence.ModelMaps
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

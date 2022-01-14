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
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.MongoDB.Bson;
using Etherna.MongoDB.Bson.Serialization.Serializers;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;
using Etherna.MongODM.Core.Serialization.Serializers;

namespace Etherna.BeehiveManager.Persistence.ModelMaps
{
    class BeeNodeMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.SchemaRegistry.AddModelMapsSchema<BeeNode>("6b94df32-034f-46f9-a5c1-239905ad5d07");

            // Aggregate models.
            dbContext.SchemaRegistry.AddModelMapsSchema<BeeNodeAddresses>("b4fc3145-6864-43d0-8ba5-c43f36877519");
            dbContext.SchemaRegistry.AddModelMapsSchema<BeeNodeStatus>("e86940fb-0eee-4cea-bf01-187738325976");
        }

        /// <summary>
        /// A minimal serialized with only id
        /// </summary>
        public static ReferenceSerializer<BeeNode, string> ReferenceSerializer(
            IDbContext dbContext,
            bool useCascadeDelete = false) =>
            new(dbContext, config =>
            {
                config.UseCascadeDelete = useCascadeDelete;
                config.AddModelMapsSchema<ModelBase>("e5d93371-e1a7-4ff3-b947-a4862c40d938");
                config.AddModelMapsSchema<EntityModelBase>("a48cf8b2-1b18-450d-afc1-4094ce23ba78", mm => { });
                config.AddModelMapsSchema<EntityModelBase<string>>("1a7fb389-fd58-4ad6-82b5-b687273bc5ab", mm =>
                {
                    mm.MapIdMember(m => m.Id);
                    mm.IdMemberMap.SetSerializer(new StringSerializer(BsonType.ObjectId));
                });
                config.AddModelMapsSchema<BeeNode>("28d5e30d-c205-4440-9ba6-80505409ef8d", mm => { });
            });
    }
}

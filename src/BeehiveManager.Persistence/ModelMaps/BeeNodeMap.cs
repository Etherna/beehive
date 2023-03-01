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
using Etherna.MongoDB.Bson;
using Etherna.MongoDB.Bson.Serialization.Serializers;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;
using Etherna.MongODM.Core.Serialization.Serializers;

namespace Etherna.BeehiveManager.Persistence.ModelMaps
{
    internal sealed class BeeNodeMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.MapRegistry.AddModelMap<BeeNode>("6b94df32-034f-46f9-a5c1-239905ad5d07");
        }

        /// <summary>
        /// A minimal serializer with only id
        /// </summary>
        public static ReferenceSerializer<BeeNode, string> ReferenceSerializer(
            IDbContext dbContext) =>
            new(dbContext, config =>
            {
                config.AddModelMap<ModelBase>("e5d93371-e1a7-4ff3-b947-a4862c40d938");
                config.AddModelMap<EntityModelBase>("a48cf8b2-1b18-450d-afc1-4094ce23ba78", mm => { });
                config.AddModelMap<EntityModelBase<string>>("1a7fb389-fd58-4ad6-82b5-b687273bc5ab", mm =>
                {
                    mm.MapIdMember(m => m.Id);
                    mm.IdMemberMap.SetSerializer(new StringSerializer(BsonType.ObjectId));
                });
                config.AddModelMap<BeeNode>("28d5e30d-c205-4440-9ba6-80505409ef8d", mm => { });
            });

        /// <summary>
        /// A serializer with connection info to node
        /// </summary>
        public static ReferenceSerializer<BeeNode, string> ConnectionInfoSerializer(
            IDbContext dbContext) =>
            new(dbContext, config =>
            {
                config.AddModelMap<ModelBase>("148b3991-63da-4966-a781-30295c71fcae");
                config.AddModelMap<EntityModelBase>("774d614c-2bd2-4a51-83a7-6d0df1942216", mm => { });
                config.AddModelMap<EntityModelBase<string>>("959def90-ddab-48a7-9a0e-1917be419171", mm =>
                {
                    mm.MapIdMember(n => n.Id);
                    mm.IdMemberMap.SetSerializer(new StringSerializer(BsonType.ObjectId));
                });
                config.AddModelMap<BeeNode>("a833d25f-4613-4cbc-b36a-4cdfa62501f4", mm =>
                {
                    mm.MapMember(n => n.ConnectionScheme);
                    mm.MapMember(n => n.DebugPort);
                    mm.MapMember(n => n.GatewayPort);
                    mm.MapMember(n => n.Hostname);
                });
            });
    }
}

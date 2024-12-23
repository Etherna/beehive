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
using Etherna.MongoDB.Bson.Serialization.Serializers;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;
using Etherna.MongODM.Core.Serialization.Serializers;

namespace Etherna.Beehive.Persistence.ModelMaps
{
    internal sealed class BeeNodeMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.MapRegistry.AddModelMap<BeeNode>("6b94df32-034f-46f9-a5c1-239905ad5d07",
                mm =>
                {
                    mm.AutoMap();

                    // Set default values.
                    mm.GetMemberMap(n => n.IsBatchCreationEnabled);
                });
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
                    mm.MapMember(n => n.ConnectionString);
                });
            });
    }
}

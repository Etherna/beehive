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
    internal sealed class ChunkPinMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.MapRegistry.AddModelMap<ChunkPin>( //v0.4.0
                "832d06b1-ed82-4f4f-9df9-ad24565df38d",
                mm =>
                {
                    mm.AutoMap();
                    mm.GetMemberMap(p => p.EncryptionKey).SetElementName("EncKey");
                    mm.GetMemberMap(p => p.RecursiveEncryption).SetElementName("RecEnc");
                });
        }

        /// <summary>
        /// A minimal serializer with only id
        /// </summary>
        public static ReferenceSerializer<ChunkPin, string> ReferenceSerializer(
            IDbContext dbContext) =>
            new(dbContext, config =>
            {
                config.AddModelMap<ModelBase>("016abab0-d092-4dbc-84f5-098b09a4b704");
                config.AddModelMap<EntityModelBase>("8d77f15f-a550-4553-9c91-4ee1170c2e77", mm => { });
                config.AddModelMap<EntityModelBase<string>>("89c6bc1a-4c6d-4805-b4dc-58ce1f99abe2", mm =>
                {
                    mm.MapIdMember(m => m.Id);
                    mm.IdMemberMap.SetSerializer(new StringSerializer(BsonType.ObjectId));
                });
                config.AddModelMap<ChunkPin>("d04090e8-1246-4ab8-bd6b-a37d9339c638", mm => { });
            });
    }
}
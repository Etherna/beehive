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
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Extensions;
using Etherna.Scrinium.Core.Options;
using Etherna.Scrinium.Core.Serialization;
using Etherna.Scrinium.Core.Serialization.Serializers;

namespace Etherna.Beehive.Persistence.ModelMaps
{
    internal sealed class PinsCheckMap : IModelMapsCollector
    {
        public void Register(IDbContextEngine dbContextEngine)
        {
            dbContextEngine.MapRegistry.AddModelMap<PinCheck>( //v0.4.12
                "b3e245e7-4685-4b7e-9e00-226d5e04b285",
                mm =>
                {
                    mm.AutoMap();

                    //a deleted run takes its pin records with it
                    mm.SetMemberSerializer(c => c.ParentCheck,
                        ReferenceSerializer(dbContextEngine, OriginDeleteMode.DeleteReferencingDocument));
                });

            dbContextEngine.MapRegistry.AddModelMap<PinsCheck>( //v0.4.12
                "0a71148a-6fb7-4a97-8b8d-51e87b866870",
                mm =>
                {
                    mm.AutoMap();

                    //by name: the members of an enum are kept in alphabetical order, a new one would shift their numbers
                    mm.SetMemberSerializer(c => c.RequestedPinStates, new EnumerableSerializer<ChunkPinState>(
                        new EnumSerializer<ChunkPinState>(BsonType.String)));
                });
        }

        /// <summary>
        /// A minimal serializer with only id
        /// </summary>
        /// <param name="originDelete">How the documents hosting the reference react when the run is deleted</param>
        public static ReferenceSerializer<PinsCheck, string> ReferenceSerializer(
            IDbContextEngine dbContextEngine,
            OriginDeleteMode originDelete) =>
            new(dbContextEngine, config =>
            {
                config.OriginDelete = originDelete;

                config.AddModelMap<ModelBase>("fc2770d1-5731-4056-9083-c8af8123dc56");
                config.AddModelMap<EntityModelBase>("4a206402-743e-41a6-aa26-aade8000b9e9", mm => { });
                config.AddModelMap<EntityModelBase<string>>("2220d59c-2f68-45f7-909d-2d4d6e8021ec", mm =>
                {
                    mm.MapIdMember(m => m.Id);
                    mm.IdMemberMap.SetSerializer(new StringSerializer(BsonType.ObjectId));
                });
                config.AddModelMap<PinsCheck>("71627fbd-87d3-46f2-9f48-bfa786655758", mm => { });
            });
    }
}

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
    internal sealed class NodePinsImportMap : IModelMapsCollector
    {
        public void Register(IDbContextEngine dbContextEngine)
        {
            dbContextEngine.MapRegistry.AddModelMap<NodePinImport>( //v0.4.11
                "529f1657-29f4-4c82-9355-6fa99750ad9d",
                mm =>
                {
                    mm.AutoMap();

                    //a deleted run takes its pin records with it
                    mm.SetMemberSerializer(r => r.Import,
                        ReferenceSerializer(dbContextEngine, OriginDeleteMode.DeleteReferencingDocument));
                });

            dbContextEngine.MapRegistry.AddModelMap<NodePinsImport>(
                "879c21f2-9ad8-4776-a904-605c26afe6dc"); //v0.4.11
        }

        /// <summary>
        /// A minimal serializer with only id
        /// </summary>
        /// <param name="originDelete">How the documents hosting the reference react when the run is deleted</param>
        public static ReferenceSerializer<NodePinsImport, string> ReferenceSerializer(
            IDbContextEngine dbContextEngine,
            OriginDeleteMode originDelete) =>
            new(dbContextEngine, config =>
            {
                config.OriginDelete = originDelete;

                config.AddModelMap<ModelBase>("6c366fab-fbbe-4a97-8601-06bb363478d5");
                config.AddModelMap<EntityModelBase>("f0cecc65-0ad8-47d9-bbd1-71340fcc5958", mm => { });
                config.AddModelMap<EntityModelBase<string>>("2a6b48e2-9937-4abf-9498-8db8832f7724", mm =>
                {
                    mm.MapIdMember(m => m.Id);
                    mm.IdMemberMap.SetSerializer(new StringSerializer(BsonType.ObjectId));
                });
                config.AddModelMap<NodePinsImport>("a6f656c8-f1b7-4695-9c08-8ac2c380ba98", mm => { });
            });
    }
}

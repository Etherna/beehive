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

using Etherna.Beehive.Persistence.Serializers;
using Etherna.BeeNet.Models;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;

namespace Etherna.Beehive.Persistence.ModelMaps
{
    internal sealed class BeeNetMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.MapRegistry.AddCustomSerializerMap<PostageBatchId>(new PostageBatchIdSerializer()); //v0.4.0
            dbContext.MapRegistry.AddCustomSerializerMap<SwarmAddress>(new SwarmAddressSerializer()); //v0.4.0
            dbContext.MapRegistry.AddCustomSerializerMap<SwarmHash>(new SwarmHashSerializer()); //v0.4.0
            dbContext.MapRegistry.AddCustomSerializerMap<SwarmReference>(new SwarmReferenceSerializer()); //v0.4.1
            dbContext.MapRegistry.AddCustomSerializerMap<SwarmUri>(new SwarmUriSerializer()); //v0.4.0
        }
    }
}
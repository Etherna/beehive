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
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;
using PostageStamp = Etherna.Beehive.Domain.Models.PostageStamp;

namespace Etherna.Beehive.Persistence.ModelMaps
{
    internal sealed class PostageBatchMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.MapRegistry.AddModelMap<PostageBatchCache>(
                "38f60c18-d20c-4d24-a619-2af9d7a5119f"); //v0.4.0
            
            dbContext.MapRegistry.AddModelMap<PostageStamp>( //v0.4.0
                "a7a3075b-ab81-4bd0-8a5e-a35a5a576a71");
        }
    }
}
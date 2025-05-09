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

using Etherna.Beehive.Services.Domain;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class PssControllerService(
        IPostageBatchService postageBatchService)
        : IPssControllerService
    {
        public async Task<IResult> SendPssMessageAsync(
            string topic,
            string targets,
            PostageBatchId batchId,
            HttpContext httpContext)
        {
            // Select node and forward request.
            var node = await postageBatchService.TryGetPostageBatchOwnerNodeAsync(batchId);
            if (node == null)
                throw new KeyNotFoundException();
            // return await node.ForwardRequestAsync(forwarder, httpContext);

            throw new NotImplementedException();
        }
    }
}
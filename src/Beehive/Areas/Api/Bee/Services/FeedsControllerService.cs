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

using Etherna.Beehive.Extensions;
using Etherna.Beehive.HttpTransformers;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class FeedsControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IHttpForwarder forwarder)
        : IFeedsControllerService
    {
        public async Task<IResult> CreateFeedRootManifestAsync(
            string owner,
            string topic,
            PostageBatchId batchId,
            HttpContext httpContext)
        {
            // Select node and forward request.
            var node = beeNodeLiveManager.TryGetPostageBatchOwnerNode(batchId);
            if (node == null)
                throw new KeyNotFoundException();
            return await node.ForwardRequestAsync(forwarder, httpContext);
        }

        public async Task<IResult> FindFeedUpdateAsync(string owner, string topic, HttpContext httpContext)
        {
            // Select node and forward request.
            var node = await beeNodeLiveManager.SelectHealthyNodeAsync();
            return await node.ForwardRequestAsync(
                forwarder,
                httpContext,
                new DownloadHttpTransformer(forceNoCache: true));
        }
    }
}
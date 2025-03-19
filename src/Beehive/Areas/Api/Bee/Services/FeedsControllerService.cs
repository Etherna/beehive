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
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class FeedsControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IHttpForwarder forwarder)
        : IFeedsControllerService
    {
        public Task<IActionResult> CreateFeedRootManifestAsync(
            EthAddress owner,
            string topic,
            SwarmFeedType type,
            PostageBatchId batchId,
            bool pinContent,
            HttpContext httpContext)
        {
            throw new NotImplementedException();
        }

        public async Task<IResult> FindFeedUpdateAsync(
            EthAddress owner,
            string topic,
            HttpContext httpContext)
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
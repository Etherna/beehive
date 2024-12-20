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

using Microsoft.AspNetCore.Http;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Etherna.Beehive.HttpTransformers
{
    public class DownloadHttpTransformer : HttpTransformer
    {
        public override async ValueTask<bool> TransformResponseAsync(
            HttpContext httpContext,
            HttpResponseMessage? proxyResponse,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));
            
            var result = await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
            if (!result)
                return false;
            
            // Set no cache in case of a feed response.
            if (httpContext.Response.Headers.TryGetValue("swarm-feed-index", out _))
                httpContext.Response.Headers["Cache-Control"] = "no-cache";

            return true;
        }
    }
}
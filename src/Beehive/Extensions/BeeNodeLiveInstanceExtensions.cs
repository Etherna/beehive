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

using Etherna.Beehive.Services.Utilities.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Etherna.Beehive.Extensions
{
    public static class BeeNodeLiveInstanceExtensions
    {
        public static async Task<IResult> ForwardRequestAsync(
            this BeeNodeLiveInstance node,
            IHttpForwarder forwarder,
            HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(node, nameof(node));
            ArgumentNullException.ThrowIfNull(forwarder, nameof(forwarder));
            ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));

            using var socketsHttpHandler = new SocketsHttpHandler();
            using var httpClient = new HttpMessageInvoker(socketsHttpHandler);

            var error = await forwarder.SendAsync(
                httpContext,
                node.Client.BeeUrl.ToString(),
                httpClient);

            if (error != ForwarderError.None)
            {
                httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
                await httpContext.Response.WriteAsync("An error occurred while forwarding the request to bee node.");
                return Results.StatusCode(StatusCodes.Status502BadGateway);
            }

            return null!; //response handled by yarp
        }
    }
}
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
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api
{
    /// <summary>
    /// Answers an exception escaping an api endpoint as <see cref="ExceptionHandler"/> answers one thrown by its
    /// handler: a result executes after the handler returned, and a lazily read content fails at the first read of
    /// its body, whatever range was asked. Until the first byte is written the response hasn't started and is
    /// replaced by the mapped one; after, the client already has a body cut short: the failure is logged and the
    /// connection aborted. Outside the api, and for a request its client abandoned, the exception passes through
    /// </summary>
    public sealed class ApiExceptionMiddleware(RequestDelegate next)
    {
        // Methods.
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Every exception of the api has an answer in its mapping")]
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // The api version of the endpoint, by the marker of its route group: null outside the api.
            var metadata = context.GetEndpoint()?.Metadata;
            ApiVersion? apiVersion = metadata switch
            {
                null => null,
                _ when metadata.GetMetadata<BeehiveApiMarker>() is not null => ApiVersion.BeehiveV04,
                _ when metadata.GetMetadata<SwarmApiMarker>() is not null ||
                       metadata.GetMetadata<SwarmV1ApiMarker>() is not null => ApiVersion.Swarm,
                _ => null
            };

            try
            {
                await next(context);
            }
            catch (Exception e) when (apiVersion.HasValue && !context.RequestAborted.IsCancellationRequested)
            {
                var result = ExceptionHandler.GetResult(apiVersion.Value, e);
                if (context.Response.HasStarted)
                {
                    ExceptionHandler.LogAborted(e, context.Request.Path, result);
                    context.Abort();
                    return;
                }

                ExceptionHandler.LogAnswered(e, result);
                context.Response.Clear();
                await result.ExecuteAsync(context);
            }
        }
    }
}

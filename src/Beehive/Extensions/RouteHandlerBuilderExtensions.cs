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

using Etherna.Beehive.Configs.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Extensions
{
    public static class RouteHandlerBuilderExtensions
    {
        public static RouteHandlerBuilder AcceptsUnrestricted<TRequest>(
            this RouteHandlerBuilder builder,
            string contentType,
            params string[] additionalContentTypes)
            where TRequest : notnull
        {
            var allContentTypes = additionalContentTypes.Prepend(contentType).ToArray();
            return builder.WithMetadata(new AcceptsUnrestrictedMetadata(allContentTypes, typeof (TRequest)));
        }

        public static RouteHandlerBuilder FilterRequestSizeLimit(
            this RouteHandlerBuilder builder,
            long bodySize) =>
            builder.AddEndpointFilter(async (context, next) =>
            {
                var httpContext = context.HttpContext;
                var maxBodySize = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

                if (maxBodySize is not null && !maxBodySize.IsReadOnly)
                    maxBodySize.MaxRequestBodySize = bodySize;

                return await next(context);
            });

        public static RouteHandlerBuilder FilterRequireAtLeastOneHeader(
            this RouteHandlerBuilder builder,
            params string[] headerNames) =>
            builder.AddEndpointFilter((context, next) =>
            {
                var headers = context.HttpContext.Request.Headers;
                if (headerNames.Any(headerName => headers.ContainsKey(headerName)))
                    return next(context);

                return ValueTask.FromResult<object?>(
                    Results.BadRequest(new
                    {
                        Error = $"At least one of the following headers is required: {string.Join(", ", headerNames)}"
                    }));
            });
        
        public static RouteHandlerBuilder IsDeprecated(this RouteHandlerBuilder builder, string? message = null) =>
            builder.WithMetadata(new DeprecatedEndpointMetadata(message));
        
        /// <summary>
        /// Required because of https://github.com/dotnet/aspnetcore/issues/43330
        /// </summary>
        public static RouteHandlerBuilder NotProduces200(this RouteHandlerBuilder builder) =>
            builder.WithMetadata(new RemoveResponse200EndpointMetadata());
    }
}
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Etherna.Beehive.Configs.DebugPages
{
    /// <summary>
    /// Development only pages: <c>/debug/config</c> prints the whole configuration of the host,
    /// <c>/debug/request</c> the headers and the values of the request as the host received it.
    /// </summary>
    public sealed class DebugPagesMiddleware(RequestDelegate next)
    {
        // Consts.
        private const string ConfigurationPagePath = "/debug/config";
        private const string RequestPagePath = "/debug/request";

        // Methods.
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            string viewName;
            object model;
            if (context.Request.Path == ConfigurationPagePath)
            {
                viewName = "ConfigurationPage";
                model = context.RequestServices.GetRequiredService<IConfiguration>();
            }
            else if (context.Request.Path == RequestPagePath)
            {
                viewName = "RequestPage";
                model = context.Request;
            }
            else
            {
                await next(context);
                return;
            }

            var executor = context.RequestServices.GetRequiredService<IActionResultExecutor<ViewResult>>();
            await executor.ExecuteAsync(
                new ActionContext(context, context.GetRouteData(), new ActionDescriptor()),
                new ViewResult
                {
                    ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                    {
                        Model = model
                    },
                    ViewName = $"~/Configs/DebugPages/{viewName}.cshtml"
                });
        }
    }
}

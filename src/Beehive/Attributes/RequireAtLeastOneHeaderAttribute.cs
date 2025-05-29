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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace Etherna.Beehive.Attributes
{
    public sealed class RequireAtLeastOneHeaderAttribute(params string[] headerNames)
        : ActionFilterAttribute
    {
        // Properties.
        public string[] HeaderNames { get; } = headerNames;
        
        // Methods.
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            
            var headerExists = false;

            foreach (var headerName in HeaderNames)
            {
                if (context.HttpContext.Request.Headers.ContainsKey(headerName))
                {
                    headerExists = true;
                    break;
                }
            }

            if (!headerExists)
            {
                context.Result = new BadRequestObjectResult(
                    new { Error = $"At least one of the following headers is required: {string.Join(", ", HeaderNames)}" });
            }
        }
    }
}
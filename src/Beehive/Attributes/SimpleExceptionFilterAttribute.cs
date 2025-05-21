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

using Etherna.Beehive.Domain.Exceptions;
using Etherna.BeeNet.Exceptions;
using Etherna.MongODM.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;
using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Attributes
{
    public sealed class SimpleExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));

            // Log exception.
            Log.Warning(context.Exception, "API exception");

            context.Result = context.Exception switch
            {
                // Error code 400.
                ArgumentException _ or
                FormatException _ or
                InvalidOperationException _ or
                MongodmInvalidEntityTypeException _ => new BadRequestObjectResult(context.Exception.Message),

                // Error code 401.
                UnauthorizedAccessException _ => new UnauthorizedResult(),

                // Error code 404.
                BeeNetApiException { StatusCode: 404 } _ or
                KeyNotFoundException _ or
                MongodmEntityNotFoundException _ => new NotFoundObjectResult(context.Exception.Message),
                
                // Error code 423.
                ResourceLockException _ => new StatusCodeResult(423),

                // Error code 503.
                BeeNetApiException _ => new StatusCodeResult(503),

                // Error code 500.
                _ => new StatusCodeResult(500),
            };
        }
    }
}

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

using Etherna.Beehive.Areas.Api.Bee.DtoModels;
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
    public sealed class BeeExceptionFilterAttribute : ExceptionFilterAttribute
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
                    MongodmInvalidEntityTypeException _ =>
                    new JsonResult(new BeeErrorDto(400, context.Exception.Message)) { StatusCode = 400 },

                // Error code 401.
                UnauthorizedAccessException _ =>
                    new JsonResult(new BeeErrorDto(401, context.Exception.Message)) { StatusCode = 401 },

                // Error code 404.
                BeeNetApiException { StatusCode: 404 } _ or
                    KeyNotFoundException _ or
                    MongodmEntityNotFoundException _ =>
                    new JsonResult(new BeeErrorDto(404, context.Exception.Message)) { StatusCode = 404 },

                // Error code 423.
                ResourceLockException _ =>
                    new JsonResult(new BeeErrorDto(423, context.Exception.Message)) { StatusCode = 423 },

                // Error code 503.
                BeeNetApiException _ =>
                    new JsonResult(new BeeErrorDto(503, context.Exception.Message)) { StatusCode = 503 },

                // Error code 500.
                _ => new JsonResult(new BeeErrorDto(500, context.Exception.Message)) { StatusCode = 500 },
            };
        }
    }
}

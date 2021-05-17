//   Copyright 2021-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.MongODM.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Attributes
{
    public sealed class SimpleExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

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
                KeyNotFoundException _ or
                MongodmEntityNotFoundException _ => new NotFoundObjectResult(context.Exception.Message),

                // Error code 503.
                BeeNetDebugApiException _ or
                BeeNetGatewayApiException _ => new StatusCodeResult(503),

                // Error code 500.
                _ => new StatusCodeResult(500),
            };
        }
    }
}

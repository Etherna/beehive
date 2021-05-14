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

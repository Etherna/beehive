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

            switch (context.Exception)
            {
                // Error code 400.
                case ArgumentException _:
                case FormatException _:
                case InvalidOperationException _:
                case MongodmInvalidEntityTypeException _:
                    context.Result = new BadRequestObjectResult(context.Exception.Message);
                    break;

                // Error code 401.
                case UnauthorizedAccessException _:
                    context.Result = new UnauthorizedResult();
                    break;

                // Error code 404.
                case KeyNotFoundException _:
                case MongodmEntityNotFoundException _:
                    context.Result = new NotFoundObjectResult(context.Exception.Message);
                    break;

                // Error code 503.
                case BeeNetDebugApiException _:
                case BeeNetGatewayApiException _:
                    context.Result = new StatusCodeResult(503);
                    break;
            }
        }
    }
}

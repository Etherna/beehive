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
using Etherna.Beehive.Services.Extensions;
using Etherna.Scrinium.Core.Exceptions;
using Etherna.SwarmSdk.Exceptions;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api
{
    public static class ExceptionHandler
    {
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        public static async Task<IResult> RunAsync(ApiVersion apiVersion, Func<Task<IResult>> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            
            try
            {
                return await action();
            }
            catch (Exception e)
            {
                var result = e switch
                {
                    // Error code 400.
                    ArgumentException or
                    FormatException or
                    InvalidDataException or
                    ScriniumInvalidEntityTypeException or
                    SwarmChunkTypeException =>
                        ErrorResults.GetBadRequestErrorResult(apiVersion),
                    
                    BadHttpRequestException =>
                        e.Message == ErrorResults.BatchNotUsableErrorMessage ? //special case
                            ErrorResults.GetBadRequestErrorResult(apiVersion, ErrorResults.BatchNotUsableErrorMessage) :
                            ErrorResults.GetBadRequestErrorResult(apiVersion),

                    // Error code 401.
                    UnauthorizedAccessException =>
                        ErrorResults.GetUnauthorizedErrorResult(apiVersion),

                    // Error code 404.
                    PostageBatchNotFoundException => //bee's upload answer for a batch its node doesn't have
                        ErrorResults.GetNotFoundErrorResult(apiVersion, ErrorResults.BatchNotFoundErrorMessage),
                    SwarmSdkApiException { StatusCode: 404 } or
                    KeyNotFoundException or
                    ScriniumEntityNotFoundException =>
                        ErrorResults.GetNotFoundErrorResult(apiVersion),

                    // Error code 422.
                    PostageBatchNotUsableException => //bee's upload answer for a batch not usable yet
                        ErrorResults.GetUnprocessableEntityErrorResult(apiVersion, ErrorResults.BatchNotUsableYetErrorMessage),

                    // Error code 423.
                    ResourceLockException =>
                        ErrorResults.GetLockedErrorResult(apiVersion),
                    
                    // Error code 503.
                    BeeNodeUnavailableException => //the message is the node's reason
                        ErrorResults.GetServiceUnavailableErrorResult(apiVersion, e.Message),
                    SwarmSdkApiException apiException => //the body carries the node's own reason
                        ErrorResults.GetServiceUnavailableErrorResult(apiVersion, apiException.GetBeeNodeFailureReason()),
                        
                    // Error code 500.
                    _ => ErrorResults.GetInternalServerErrorResult(apiVersion)
                };

                // Log the exception by the answer: a failure of beehive is an error, one of a node a warning,
                // a client error a trace, not a problem of the server.
                var statusCode = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status500InternalServerError;
                Log.Write(GetLogEventLevel(statusCode), e, "API exception answered with status {StatusCode}", statusCode);

                return result;
            }
        }

        // Helpers.
        private static LogEventLevel GetLogEventLevel(int statusCode) =>
            statusCode switch
            {
                StatusCodes.Status401Unauthorized => LogEventLevel.Warning,
                StatusCodes.Status503ServiceUnavailable => LogEventLevel.Warning,
                >= StatusCodes.Status500InternalServerError => LogEventLevel.Error,
                _ => LogEventLevel.Debug
            };
    }
}
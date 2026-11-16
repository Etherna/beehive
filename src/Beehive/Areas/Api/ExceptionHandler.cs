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
using Etherna.MongoDB.Driver;
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
    /// <summary>
    /// The answer of the api to an exception: the status by the exception, the body by the api version, the log by
    /// the status. <see cref="RunAsync"/> answers what a handler throws; <see cref="ApiExceptionMiddleware"/> what
    /// a result throws while it executes, after the handler returned
    /// </summary>
    public static class ExceptionHandler
    {
        // Methods.
        public static IResult GetResult(ApiVersion apiVersion, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return exception switch
            {
                // Error code 400.
                ArgumentException or
                FormatException or
                InvalidDataException or
                ScriniumInvalidEntityTypeException or
                SwarmChunkTypeException =>
                    ErrorResults.GetBadRequestErrorResult(apiVersion),

                BadHttpRequestException =>
                    exception.Message == ErrorResults.BatchNotUsableErrorMessage ? //special case
                        ErrorResults.GetBadRequestErrorResult(apiVersion, ErrorResults.BatchNotUsableErrorMessage) :
                        ErrorResults.GetBadRequestErrorResult(apiVersion),

                // Error code 401.
                UnauthorizedAccessException =>
                    ErrorResults.GetUnauthorizedErrorResult(apiVersion),

                // Error code 404.
                PostageBatchNotFoundException => //bee's upload answer for a batch its node doesn't have
                    ErrorResults.GetNotFoundErrorResult(apiVersion, ErrorResults.BatchNotFoundErrorMessage),
                SwarmChunkChildrenNotFoundException or //chunks of a content a read couldn't fetch: the message names them and the chunk they belong to
                SwarmChunksNotFoundException =>
                    ErrorResults.GetNotFoundErrorResult(apiVersion, exception.Message),
                SwarmSdkApiException { StatusCode: 404 } or
                KeyNotFoundException or
                ScriniumEntityNotFoundException =>
                    ErrorResults.GetNotFoundErrorResult(apiVersion),

                // Error code 409.
                /* A transaction that lost a write conflict with a concurrent one, after the retries of the
                 * db context: the request is legitimate and the client can repeat it, so answer a conflict
                 * instead of a server error. */
                MongoException mongoException when mongoException.HasErrorLabel("TransientTransactionError") =>
                    ErrorResults.GetErrorResult(apiVersion, StatusCodes.Status409Conflict, ErrorResults.WriteConflictErrorMessage),

                // Error code 422.
                PostageBatchNotUsableException => //bee's upload answer for a batch not usable yet
                    ErrorResults.GetUnprocessableEntityErrorResult(apiVersion, ErrorResults.BatchNotUsableYetErrorMessage),
                SwarmChunkFormatException => //a chunk that is not well formed, or a tree whose chunks don't agree: bad data stored by a client, the message names the chunk and what is wrong
                    ErrorResults.GetUnprocessableEntityErrorResult(apiVersion, exception.Message),

                // Error code 423.
                ResourceLockException =>
                    ErrorResults.GetLockedErrorResult(apiVersion),

                // Error code 504. Before the node failures predicate below, which counts a timeout too.
                TimeoutException => //the node didn't complete the call within the bound of its operation: the message names the call and the bound
                    ErrorResults.GetGatewayTimeoutErrorResult(apiVersion, exception.Message),

                // Error code 503.
                BeeNodeUnavailableException => //the message is the node's reason
                    ErrorResults.GetServiceUnavailableErrorResult(apiVersion, exception.Message),
                _ when exception.IsBeeNodeCallFailure() => //a node that answered an error or refused the connection: its reason, never a failure of beehive
                    ErrorResults.GetServiceUnavailableErrorResult(apiVersion, exception.GetBeeNodeFailureReason()),

                // Error code 500.
                _ => ErrorResults.GetInternalServerErrorResult(apiVersion)
            };
        }

        /// <summary>
        /// Log an exception the response can't answer any more, having started: the client got a body cut short,
        /// a warning at least whatever status would have been answered
        /// </summary>
        public static void LogAborted(Exception exception, PathString path, IResult result)
        {
            var statusCode = GetStatusCode(result);
            var level = GetLogEventLevel(statusCode);
            Log.Write(
                level < LogEventLevel.Warning ? LogEventLevel.Warning : level,
                exception,
                "API exception after the response of {Path} started: connection aborted instead of answering status {StatusCode}",
                path,
                statusCode);
        }

        /// <summary>
        /// Log an exception by its answer: a failure of beehive is an error, one of a node a warning, a client error
        /// a trace, not a problem of the server
        /// </summary>
        public static void LogAnswered(Exception exception, IResult result)
        {
            var statusCode = GetStatusCode(result);
            Log.Write(GetLogEventLevel(statusCode), exception, "API exception answered with status {StatusCode}", statusCode);
        }

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
                var result = GetResult(apiVersion, e);
                LogAnswered(e, result);
                return result;
            }
        }

        // Helpers.
        private static LogEventLevel GetLogEventLevel(int statusCode) =>
            statusCode switch
            {
                StatusCodes.Status401Unauthorized => LogEventLevel.Warning,
                StatusCodes.Status409Conflict => LogEventLevel.Warning, //a write conflict the db context couldn't retry away
                StatusCodes.Status503ServiceUnavailable => LogEventLevel.Warning,
                StatusCodes.Status504GatewayTimeout => LogEventLevel.Warning,
                >= StatusCodes.Status500InternalServerError => LogEventLevel.Error,
                _ => LogEventLevel.Debug
            };

        private static int GetStatusCode(IResult result) =>
            (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status500InternalServerError;
    }
}

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
using Etherna.MongODM.Core.Exceptions;
using Etherna.SwarmSdk.Exceptions;
using Microsoft.AspNetCore.Http;
using Serilog;
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
                // Log exception.
                Log.Warning(e, "API exception");

                switch (e)
                {
                    // Error code 400.
                    case ArgumentException:
                    case FormatException:
                    case InvalidDataException:
                    case MongodmInvalidEntityTypeException:
                    case SwarmChunkTypeException:
                        return ErrorResults.GetBadRequestErrorResult(apiVersion);
                    
                    case BadHttpRequestException:
                        return e.Message == ErrorResults.BatchNotUsableErrorMessage ? //special case
                            ErrorResults.GetBadRequestErrorResult(apiVersion, ErrorResults.BatchNotUsableErrorMessage) :
                            ErrorResults.GetBadRequestErrorResult(apiVersion);

                    // Error code 401.
                    case UnauthorizedAccessException:
                        return ErrorResults.GetUnauthorizedErrorResult(apiVersion);

                    // Error code 404.
                    case SwarmSdkApiException { StatusCode: 404 }:
                    case KeyNotFoundException:
                    case MongodmEntityNotFoundException:
                        return ErrorResults.GetNotFoundErrorResult(apiVersion);

                    // Error code 423.
                    case ResourceLockException:
                        return ErrorResults.GetLockedErrorResult(apiVersion);
                    
                    // Error code 503.
                    case SwarmSdkApiException:
                        return ErrorResults.GetServiceUnavailableErrorResult(apiVersion);
                        
                    // Error code 500.
                    case InvalidOperationException:
                    default:
                        return ErrorResults.GetInternalServerErrorResult(apiVersion);
                }
            }
        }
    }
}
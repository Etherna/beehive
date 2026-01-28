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

using Etherna.Beehive.Areas.Api.DtoModels;
using Etherna.Beehive.Configs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Etherna.Beehive.Areas.Api
{
    public static class ErrorResults
    {
        // Methods.
        public static IResult GetBadRequestErrorResult(
            ApiVersion version,
            string? customMessage = null) =>
            GetErrorResult(version, StatusCodes.Status400BadRequest, customMessage ?? "Bad request");

        public static IResult GetErrorResult(
            ApiVersion version,
            int statusCode,
            string message) =>
            version switch
            {
                ApiVersion.BeehiveV04 => Results.Json(
                    new ObjectResult(message) { StatusCode = statusCode },
                    CommonConsts.BeehiveV04JsonSerializerOptions,
                    statusCode: statusCode),
                
                ApiVersion.Swarm => Results.Json(
                    new BeeErrorDto(statusCode, message),
                    CommonConsts.SwarmJsonSerializerOptions,
                    statusCode: statusCode),
                
                _ => throw new ArgumentOutOfRangeException(nameof(version), version, null)
            };
        
        public static IResult GetInternalServerErrorResult(
            ApiVersion version,
            string? customMessage = null) =>
            GetErrorResult(version, StatusCodes.Status500InternalServerError, customMessage ?? "Internal server error");

        public static IResult GetLockedErrorResult(
            ApiVersion version,
            string? customMessage = null) =>
            GetErrorResult(version, StatusCodes.Status423Locked, customMessage ?? "Locked");

        public static IResult GetNotFoundErrorResult(
            ApiVersion version,
            string? customMessage = null) =>
            GetErrorResult(version, StatusCodes.Status404NotFound, customMessage ?? "Not found");
        
        public static IResult GetServiceUnavailableErrorResult(
            ApiVersion version,
            string? customMessage = null) =>
            GetErrorResult(version, StatusCodes.Status503ServiceUnavailable, customMessage ?? "Service unavailable");
        
        public static IResult GetUnauthorizedErrorResult(
            ApiVersion version,
            string? customMessage = null) =>
            GetErrorResult(version, StatusCodes.Status401Unauthorized, customMessage ?? "Unauthorized request");
    }
}
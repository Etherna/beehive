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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Etherna.Beehive.Configs
{
    /// <summary>
    /// Minimal API binds enum header/query parameters case-sensitively: only the exact PascalCase
    /// member name or the integer value bind, while any other casing is rejected with 400. Bee
    /// documents these values in lowercase (and as integers), so to stay compatible this middleware
    /// normalizes enum-typed header/query parameter values to their canonical member name before
    /// binding, making the binding accept any casing and the integer form.
    /// </summary>
    public sealed class CaseInsensitiveEnumBindingMiddleware(RequestDelegate next)
    {
        // Fields.
        private static readonly ConcurrentDictionary<MethodInfo, EnumParameter[]> ParametersCache = new();

        // Methods.
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var method = context.GetEndpoint()?.Metadata.GetMetadata<MethodInfo>();
            if (method is not null)
                foreach (var parameter in ParametersCache.GetOrAdd(method, BuildEnumParameters))
                {
                    var error = NormalizeOrValidate(context, parameter);
                    if (error is not null)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        context.Response.ContentType = "text/plain; charset=utf-8";
                        await context.Response.WriteAsync(error);
                        return;
                    }
                }

            await next(context);
        }

        // Helpers.
        private static EnumParameter[] BuildEnumParameters(MethodInfo method)
        {
            var result = new List<EnumParameter>();
            foreach (var parameter in method.GetParameters())
            {
                var enumType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
                if (!enumType.IsEnum)
                    continue;

                var fromHeader = parameter.GetCustomAttribute<FromHeaderAttribute>();
                if (fromHeader is not null)
                {
                    result.Add(new EnumParameter(EnumParameterSource.Header, fromHeader.Name ?? parameter.Name!, enumType));
                    continue;
                }

                var fromQuery = parameter.GetCustomAttribute<FromQueryAttribute>();
                if (fromQuery is not null)
                    result.Add(new EnumParameter(EnumParameterSource.Query, fromQuery.Name ?? parameter.Name!, enumType));
            }
            return result.ToArray();
        }

        // Returns an error message if the supplied value is out of range, otherwise null (rewriting
        // the request value to the canonical member name when it resolves to a defined member).
        private static string? NormalizeOrValidate(HttpContext context, EnumParameter parameter)
        {
            switch (parameter.Source)
            {
                case EnumParameterSource.Header:
                    if (context.Request.Headers.TryGetValue(parameter.Name, out var headerValue))
                    {
                        var (normalized, error) = Resolve(parameter.EnumType, headerValue.ToString());
                        if (error is not null)
                            return error;
                        if (normalized is not null)
                            context.Request.Headers[parameter.Name] = normalized;
                    }
                    break;

                case EnumParameterSource.Query:
                    if (context.Request.Query.TryGetValue(parameter.Name, out var queryValue))
                    {
                        var (normalized, error) = Resolve(parameter.EnumType, queryValue.ToString());
                        if (error is not null)
                            return error;
                        if (normalized is not null)
                        {
                            var query = context.Request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                            query[parameter.Name] = new StringValues(normalized);
                            context.Request.Query = new QueryCollection(query);
                        }
                    }
                    break;
            }
            return null;
        }

        private static (string? Normalized, string? Error) Resolve(Type enumType, string rawValue)
        {
            // Empty: let the framework apply the parameter default.
            // Not a name nor an integer: leave it to the framework's binding error.
            if (string.IsNullOrEmpty(rawValue) ||
                !Enum.TryParse(enumType, rawValue, ignoreCase: true, out var parsed))
                return (null, null);

            // Integer outside the defined members: reject (Enum.TryParse accepts any numeric value).
            if (!Enum.IsDefined(enumType, parsed!))
                return (null, $"Invalid value '{rawValue}' for enum parameter. " +
                              $"Allowed values: {EnumApiConventions.FormatAllowedValues(enumType)}.");

            return (parsed!.ToString(), null);
        }

        // Nested types.
        private enum EnumParameterSource { Header, Query }

        private sealed record EnumParameter(EnumParameterSource Source, string Name, Type EnumType);
    }
}

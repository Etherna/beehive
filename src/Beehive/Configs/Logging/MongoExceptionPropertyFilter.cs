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
using Serilog.Exceptions.Filters;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.Beehive.Configs.Logging
{
    /// <summary>
    /// Keeps only the scalar properties of the Mongo driver exceptions in the exception details of a log event:
    /// codes, names, messages, flags and error labels survive, while the object graphs the driver exposes
    /// (<c>Command</c>, <c>Result</c>, <c>Query</c>, <c>WriteError</c>, <c>ConnectionId</c>, ...) are dropped.
    /// Destructured by reflection, those graphs expand into hundreds of nested fields that clash with the mapping
    /// of the Elasticsearch log stream, and an event carrying one never reaches the stream.
    /// The properties inherited from <see cref="Exception"/> (message, inner exception, data) are not filtered.
    /// The process loads two copies of the driver, the Etherna fork Scrinium uses and the official one Hangfire.Mongo
    /// uses, each with its own <c>MongoException</c>: the filter knows both, by name, without referencing the official
    /// driver the project doesn't depend on directly.
    /// </summary>
    internal sealed class MongoExceptionPropertyFilter : IExceptionPropertyFilter
    {
        // Methods.
        public bool ShouldPropertyBeFiltered(Exception exception, string propertyName, object? value)
        {
            ArgumentNullException.ThrowIfNull(exception);

            var driverExceptionType = DriverExceptionTypeOf(exception.GetType());
            if (driverExceptionType is null)
                return false;

            //the value is already destructured (a dictionary or a list): decide on the declared property type
            var property = exception.GetType().GetProperties().FirstOrDefault(p => p.Name == propertyName);
            return property is not null
                && driverExceptionType.IsAssignableFrom(property.DeclaringType)
                && !IsScalar(property.PropertyType);
        }

        // Helpers.
        /// <summary>
        /// The root exception type of the driver among the bases of <paramref name="exceptionType"/>, whichever copy
        /// of the driver declares it, or null when the exception is not a driver's.
        /// </summary>
        private static Type? DriverExceptionTypeOf(Type exceptionType)
        {
            for (var type = exceptionType; type is not null; type = type.BaseType)
                if (type is { Name: "MongoException", Namespace: "MongoDB.Driver" or "Etherna.MongoDB.Driver" })
                    return type;
            return null;
        }

        private static bool IsScalar(Type type) =>
            type == typeof(string)
            || type.IsValueType
            || typeof(IEnumerable<string>).IsAssignableFrom(type);
    }
}

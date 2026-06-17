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

using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace Etherna.Beehive.Configs
{
    /// <summary>
    /// Shared conventions for enum API parameters. Enum header/query parameters are documented (and
    /// serialized) with the kebab-lower-case naming policy and accept both their case-insensitive
    /// name and their integer value (see <see cref="CaseInsensitiveEnumBindingMiddleware"/>).
    /// </summary>
    public static class EnumApiConventions
    {
        // Keep in sync with the JsonStringEnumConverter naming policy registered in Program.cs.
        public static readonly JsonNamingPolicy NamingPolicy = JsonNamingPolicy.KebabCaseLower;

        /// <summary>
        /// Returns the allowed values of an enum as "name (integer)" pairs, e.g. "none (0), medium (1)".
        /// </summary>
        public static string FormatAllowedValues(Type enumType)
        {
            ArgumentNullException.ThrowIfNull(enumType);

            var underlyingType = Enum.GetUnderlyingType(enumType);
            return string.Join(", ", Enum.GetNames(enumType).Select(name =>
            {
                var value = Convert.ChangeType(Enum.Parse(enumType, name), underlyingType, CultureInfo.InvariantCulture);
                return $"{NamingPolicy.ConvertName(name)} ({value})";
            }));
        }
    }
}

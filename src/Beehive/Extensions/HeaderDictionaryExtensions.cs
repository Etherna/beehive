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
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.Beehive.Extensions
{
    public static class HeaderDictionaryExtensions
    {
        public static void ExposeHeaders(this IHeaderDictionary headers, params string[] headerNames)
        {
            ArgumentNullException.ThrowIfNull(headers);
            ArgumentNullException.ThrowIfNull(headerNames);

            // Merge with any already exposed header, additively and without duplicates, so different
            // code paths can each expose their own headers without overwriting the others'.
            var exposed = new List<string>();
            foreach (var value in headers.AccessControlExposeHeaders)
            {
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                foreach (var name in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (!exposed.Contains(name, StringComparer.OrdinalIgnoreCase))
                        exposed.Add(name);
            }
            foreach (var name in headerNames)
                if (!exposed.Contains(name, StringComparer.OrdinalIgnoreCase))
                    exposed.Add(name);

            headers.AccessControlExposeHeaders = new StringValues(exposed.ToArray());
        }

        public static void SetNoCache(this IHeaderDictionary headers)
        {
            ArgumentNullException.ThrowIfNull(headers);
            
            headers.CacheControl = new[]
            {
                "no-store",
                "no-cache",
                "must-revalidate",
                "proxy-revalidate"
            };
            headers.Expires = "0";
        }
    }
}
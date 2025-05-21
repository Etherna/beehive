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

using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Etherna.Beehive.Attributes
{
    [SuppressMessage("Design", "CA1019:Define accessors for attribute arguments")]
    public sealed class ConsumesUnrestrictedAttribute : ActionFilterAttribute
    {
        // Constructor.
        /// <summary>
        /// Creates a new instance of <see cref="ConsumesUnrestrictedAttribute"/>.
        /// <param name="additionalContentTypes">The additional list of allowed request content types.</param>
        /// </summary>
        public ConsumesUnrestrictedAttribute(params string[] additionalContentTypes)
        {
            ArgumentNullException.ThrowIfNull(additionalContentTypes);

            var contentTypes = additionalContentTypes.Append("*/*").ToArray();
            
            foreach (var contentType in contentTypes)
                MediaTypeHeaderValue.Parse(contentType);

            var mediaContentTypes = new MediaTypeCollection();
            foreach (var ct in contentTypes)
                mediaContentTypes.Add(ct);
            ContentTypes = mediaContentTypes;
        }

        // Properties.
        public MediaTypeCollection ContentTypes { get; }

        /// <summary>
        /// Gets or sets a value that determines if the request body is optional.
        /// This value is only used to specify if the request body is required in API explorer.
        /// </summary>
        public bool IsOptional { get; set; }
    }
}
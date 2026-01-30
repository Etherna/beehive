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
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Configs.OpenApi
{
    public sealed class SwarmOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(operation);
            
            // Set default response errors schema.
            foreach (var response in (operation.Responses ?? [])
                     .Where(r => int.TryParse(r.Key, out var statusCode) && statusCode is < 200 or > 299)
                     .Select(r => r.Value)
                     .OfType<OpenApiResponse>())
            {
                response.Content ??= new Dictionary<string, OpenApiMediaType>();
                if (!response.Content.ContainsKey("application/json"))
                    response.Content.Add("application/json", new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference(nameof(BeeErrorDto))
                    });
            }
            
            // Set tags.
            operation.Tags = new HashSet<OpenApiTagReference>();
            operation.Tags.Add(new OpenApiTagReference((context.Description.RelativePath ?? "")
                .Split('/').Where(s => !string.IsNullOrWhiteSpace(s) && s != "v1" && s != "ev1")
                .Select<string, string>(s => char.ToUpperInvariant(s[0]) + s[1..])
                .FirstOrDefault() ?? ""));
            
            return Task.CompletedTask;
        }
    }
}
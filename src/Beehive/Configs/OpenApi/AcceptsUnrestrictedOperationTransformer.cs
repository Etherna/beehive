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

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Configs.OpenApi
{
    public sealed class AcceptsUnrestrictedOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(context);

            // Apply configurations from AcceptsUnrestrictedMetadata, if present.
            var consumesUnrestrictedAttribute = context.Description.ActionDescriptor.EndpointMetadata
                .OfType<AcceptsUnrestrictedMetadata>().FirstOrDefault();
            if (consumesUnrestrictedAttribute != null)
            {
                operation.RequestBody = new OpenApiRequestBody
                {
                    Content = consumesUnrestrictedAttribute.ContentTypes.ToDictionary(
                        contentType => contentType,
                        _ => new OpenApiMediaType
                        {
                            Schema = consumesUnrestrictedAttribute.RequestType != null
                                ? new OpenApiSchemaReference(consumesUnrestrictedAttribute.RequestType.Name)
                                : new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
                        }),
                    Required = !consumesUnrestrictedAttribute.IsOptional
                };
            }

            return Task.CompletedTask;
        }
    }
}
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
    public sealed class DeprecatedOperationTransformer : IOpenApiOperationTransformer
    {
        public Task TransformAsync(
            OpenApiOperation operation,
            OpenApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(context);

            var deprecatedMetadata = context.Description.ActionDescriptor.EndpointMetadata
                .OfType<DeprecatedEndpointMetadata>().FirstOrDefault();
            if (deprecatedMetadata != null)
            {
                operation.Deprecated = true;
                
                var newDescription = deprecatedMetadata.Message == null ?
                    "Deprecated" :
                    $"Deprecated: {deprecatedMetadata.Message}";
                if (operation.Description != null)
                    newDescription += $" . {operation.Description}";
                
                operation.Description = newDescription;
            }
            
            return Task.CompletedTask;
        }
    }
}
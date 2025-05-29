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

using Etherna.Beehive.Attributes;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Linq;

namespace Etherna.Beehive.Configs.Swagger.OperationFilters
{
    /// <summary>
    /// Represents the Swagger/Swashbuckle operation filter used to document the implicit API version parameter.
    /// </summary>
    /// <remarks>This <see cref="IOperationFilter"/> is only required due to bugs in the <see cref="SwaggerGenerator"/>.
    /// Once they are fixed and published, this class can be removed.</remarks>
    public class SwaggerDefaultValuesFilter : IOperationFilter
    {
        /// <summary>
        /// Applies the filter to the specified operation using the given context.
        /// </summary>
        /// <param name="operation">The operation to apply the filter to.</param>
        /// <param name="context">The current operation filter context.</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));
            ArgumentNullException.ThrowIfNull(operation, nameof(operation));

            var apiDescription = context.ApiDescription;
            
            // Apply configurations from ConsumesUnrestrictedAttribute, if present.
            var consumesUnrestrictedAttribute = apiDescription.ActionDescriptor.EndpointMetadata
                .OfType<ConsumesUnrestrictedAttribute>().FirstOrDefault();
            if (consumesUnrestrictedAttribute != null)
            {
                operation.RequestBody ??= new OpenApiRequestBody();
                
                operation.RequestBody.Content.Clear();
                foreach (var contentType in consumesUnrestrictedAttribute.ContentTypes)
                    operation.RequestBody.Content.Add(contentType, new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema { Type = "string", Format = "binary"}
                    });
                operation.RequestBody.Required = !consumesUnrestrictedAttribute.IsOptional;
            }

            // Fix deprecate operation attribute.
            operation.Deprecated |= apiDescription.IsDeprecated();

            // Fix parameters.
            if (operation.Parameters != null)
            {
                // Remove default "api-version" unspecified parameters.
                operation.Parameters = operation.Parameters.Where(p => p.Name != "api-version").ToList();

                // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/412
                // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/pull/413
                foreach (var parameter in operation.Parameters)
                {
                    var description = apiDescription.ParameterDescriptions.First(p => p.Name == parameter.Name);

                    parameter.Description ??= description.ModelMetadata?.Description;

                    if (parameter.Schema.Default == null && description.DefaultValue != null)
                    {
                        parameter.Schema.Default = new OpenApiString(description.DefaultValue.ToString());
                    }

                    parameter.Required |= description.IsRequired;
                }
            }
        }
    }
}

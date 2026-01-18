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

using Etherna.BeeNet.Models;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;

namespace Etherna.Beehive.Configs.Swagger.SchemaFilters
{
    public sealed class EthTxHashSchemaFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(context);
            
            if (schema is not OpenApiSchema openApiSchema)
                return;
            
            if (context.Type == typeof(EthTxHash) || context.Type == typeof(EthTxHash?))
            {
                openApiSchema.Type = JsonSchemaType.String;
                openApiSchema.Format = null;
                openApiSchema.MinLength = EthTxHash.HashSize * 2;
                openApiSchema.MaxLength = EthTxHash.HashSize * 2;
                openApiSchema.Pattern = $"^[a-fA-F0-9]{{{EthTxHash.HashSize * 2}}}$";
            }
        }
    }
}
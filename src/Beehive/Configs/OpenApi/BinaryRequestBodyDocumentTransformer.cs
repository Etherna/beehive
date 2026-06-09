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
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Configs.OpenApi
{
    /// <summary>
    /// Emits binary request bodies inline ({ type: string, format: binary }) instead of as a
    /// $ref to the named "Stream" component schema.
    ///
    /// The built-in generator (Microsoft.AspNetCore.OpenApi) registers System.IO.Stream request
    /// bodies as a $ref to a "Stream" component. That is valid OpenAPI, but code generators such
    /// as NSwag don't recognise a $ref-to-binary as a raw stream body and instead generate a
    /// JSON-serialized DTO body, breaking uploads in generated clients. Inlining the binary schema
    /// is what the official Bee spec does and what NSwag handles correctly (raw StreamContent).
    ///
    /// A binary reference is matched by its resolved type ({ string, binary }) rather than by the
    /// component name: the built-in generator emits a reference with an empty Id whose target is the
    /// binary "Stream" schema, so name matching would miss it.
    ///
    /// Responses are intentionally left untouched: their $ref to "Stream" already maps correctly to
    /// a streaming FileResponse downstream, so the "Stream" component is still referenced and kept.
    /// </summary>
    public sealed class BinaryRequestBodyDocumentTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(document);

            if (document.Paths == null)
                return Task.CompletedTask;

            foreach (var pathItem in document.Paths.Values)
            {
                if (pathItem.Operations == null)
                    continue;

                foreach (var operation in pathItem.Operations.Values)
                {
                    var content = operation.RequestBody?.Content;
                    if (content == null)
                        continue;

                    foreach (var mediaType in content.Values)
                    {
                        if (mediaType.Schema is OpenApiSchemaReference schemaRef &&
                            schemaRef.Type == JsonSchemaType.String &&
                            schemaRef.Format == "binary")
                        {
                            mediaType.Schema = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Format = "binary"
                            };
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}

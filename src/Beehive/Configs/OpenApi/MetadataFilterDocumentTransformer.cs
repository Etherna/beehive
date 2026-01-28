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
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Configs.OpenApi
{
    public sealed partial class MetadataFilterDocumentTransformer<TMetadata> : IOpenApiDocumentTransformer
        where TMetadata : class
    {
        [GeneratedRegex(@"\{(?:\*{1,2})?([^}:?]+)(?:[?:]?[^}]*)\}", RegexOptions.IgnoreCase)]
        private static partial Regex NormalizeArgRegex();
        
        public Task TransformAsync(
            OpenApiDocument document,
            OpenApiDocumentTransformerContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(document);
            
            var pathsToRemove = new List<string>();
            foreach (var pathItem in document.Paths)
            {
                var operationsToRemove = new List<HttpMethod>();
                foreach (var operation in pathItem.Value.Operations ?? [])
                {
                    var shouldKeep = false;

                    foreach (var descriptionGroup in context.DescriptionGroups)
                    {
                        foreach (var apiDescription in descriptionGroup.Items)
                        {
                            var pathMatches = NormalizedPathsMatch(apiDescription.RelativePath, pathItem.Key);
                            var methodMatches = apiDescription.HttpMethod?.Equals(operation.Key.ToString(), StringComparison.OrdinalIgnoreCase) == true;

                            if (pathMatches && methodMatches &&
                                apiDescription.ActionDescriptor.EndpointMetadata
                                    .OfType<TMetadata>()
                                    .Any())
                            {
                                shouldKeep = true;
                                break;
                            }
                        }

                        if (shouldKeep) break;
                    }

                    if (!shouldKeep)
                        operationsToRemove.Add(operation.Key);
                }

                foreach (var operationType in operationsToRemove)
                    pathItem.Value.Operations?.Remove(operationType);

                if (pathItem.Value.Operations is null || pathItem.Value.Operations.Count == 0)
                    pathsToRemove.Add(pathItem.Key);
            }

            foreach (var path in pathsToRemove)
                document.Paths.Remove(path);

            return Task.CompletedTask;
        }
        
        private static bool NormalizedPathsMatch(string? apiDescriptionPath, string openApiPath)
        {
            if (apiDescriptionPath == null)
                return false;

            var normalizedApiPath = NormalizeArgRegex().Replace(apiDescriptionPath.TrimStart('/'), "{$1}");
            var normalizedOpenApiPath = NormalizeArgRegex().Replace(openApiPath.TrimStart('/'), "{$1}");

            return string.Equals(normalizedApiPath, normalizedOpenApiPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
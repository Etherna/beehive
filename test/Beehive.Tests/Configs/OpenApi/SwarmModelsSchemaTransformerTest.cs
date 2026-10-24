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

using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Configs.OpenApi
{
    public class SwarmModelsSchemaTransformerTest
    {
        // Consts.
        private const string DocumentName = "test";

        // Classes.
        internal sealed class ChunksDto(
            SwarmHash hash,
            IEnumerable<SwarmHash> hashes,
            IEnumerable<string> labels,
            IEnumerable<SwarmReference>? references)
        {
            public SwarmHash Hash { get; } = hash;
            public IEnumerable<SwarmHash> Hashes { get; } = hashes;
            public IEnumerable<string> Labels { get; } = labels;
            public IEnumerable<SwarmReference>? References { get; } = references;
        }

        // Tests.
        [Fact]
        public async Task ArrayDescribedByTheExporterKeepsItsItems()
        {
            // Setup.
            var schemas = await GetDocumentSchemasAsync();

            // Action.
            var labels = schemas.GetProperty(nameof(ChunksDto)).GetProperty("properties").GetProperty("labels");

            // Assert.
            Assert.Equal("string", labels.GetProperty("items").GetProperty("type").GetString());
        }

        [Theory]
        [InlineData("hash", "SwarmHash")]
        [InlineData("hashes", "SwarmHash")]
        [InlineData("references", "SwarmReference")]
        public async Task SwarmModelReferencesItsComponentAlsoAsArrayElement(string property, string component)
        {
            // Setup.
            var schemas = await GetDocumentSchemasAsync();

            // Action.
            var propertySchema = schemas.GetProperty(nameof(ChunksDto)).GetProperty("properties").GetProperty(property);

            // Assert.
            var refSchema = propertySchema.TryGetProperty("items", out var items) ? items : propertySchema;
            Assert.Equal($"#/components/schemas/{component}", refSchema.GetProperty("$ref").GetString());
            Assert.Equal("string", schemas.GetProperty(component).GetProperty("type").GetString());
        }

        // Helpers.
        private static async Task<JsonElement> GetDocumentSchemasAsync()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddOpenApi(DocumentName, options =>
                options.AddSchemaTransformer<SwarmModelsSchemaTransformer>());
            await using var app = builder.Build();
            app.MapGet("/chunks", () => new ChunksDto(SwarmHash.Zero, [], [], null));
            await app.StartAsync(); //the api explorer sees the mapped endpoints only once the app has started

            var document = await app.Services
                .GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName)
                .GetOpenApiDocumentAsync();
            var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1);
            return JsonDocument.Parse(json).RootElement.GetProperty("components").GetProperty("schemas").Clone();
        }
    }
}

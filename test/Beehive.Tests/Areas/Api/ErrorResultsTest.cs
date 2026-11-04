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
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api
{
    public class ErrorResultsTest
    {
        // Tests.

        [Fact]
        public async Task BeehiveErrorBodyIsTheMessageAsJsonString()
        {
            // Action.
            var result = ErrorResults.GetErrorResult(ApiVersion.BeehiveV04, StatusCodes.Status404NotFound, "Node not found");
            var (statusCode, contentType, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status404NotFound, statusCode);
            Assert.Equal("application/json; charset=utf-8", contentType);
            Assert.Equal(JsonValueKind.String, body.ValueKind);
            Assert.Equal("Node not found", body.GetString());
        }

        [Fact]
        public async Task SwarmErrorBodyIsBeeStatusShape()
        {
            // Action.
            var result = ErrorResults.GetErrorResult(ApiVersion.Swarm, StatusCodes.Status404NotFound, "Not found");
            var (statusCode, contentType, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status404NotFound, statusCode);
            Assert.Equal("application/json; charset=utf-8", contentType);
            Assert.Equal(JsonValueKind.Object, body.ValueKind);
            Assert.Equal(404, body.GetProperty("code").GetInt32());
            Assert.Equal("Not found", body.GetProperty("message").GetString());
        }

        // Helpers.
        private static async Task<(int StatusCode, string? ContentType, JsonElement Body)> ExecuteResultAsync(IResult result)
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            await using var bodyStream = new MemoryStream();
            httpContext.Response.Body = bodyStream;

            await result.ExecuteAsync(httpContext);

            bodyStream.Position = 0;
            using var body = await JsonDocument.ParseAsync(bodyStream);
            return (httpContext.Response.StatusCode, httpContext.Response.ContentType, body.RootElement.Clone());
        }
    }
}

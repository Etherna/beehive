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

using Etherna.SwarmSdk.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api
{
    public class ExceptionHandlerTest
    {
        // Tests.

        [Fact]
        public async Task SwarmSdkApiExceptionAnswersServiceUnavailableWithNodeReason()
        {
            // Setup.
            var exception = new SwarmSdkApiException(
                "The HTTP status code of the response was not expected (503).",
                503,
                """{"code":503,"message":"syncing in progress"}""",
                new Dictionary<string, IEnumerable<string>>(),
                null);

            // Action.
            var result = await ExceptionHandler.RunAsync(ApiVersion.Swarm, () => throw exception);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
            Assert.Equal(503, body.GetProperty("code").GetInt32());
            Assert.Equal("syncing in progress", body.GetProperty("message").GetString());
        }

        // Helpers.
        private static async Task<(int StatusCode, JsonElement Body)> ExecuteResultAsync(IResult result)
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
            return (httpContext.Response.StatusCode, body.RootElement.Clone());
        }
    }
}

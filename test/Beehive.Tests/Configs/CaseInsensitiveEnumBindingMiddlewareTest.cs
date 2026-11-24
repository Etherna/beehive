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

using Etherna.Beehive.Domain.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Configs
{
    public class CaseInsensitiveEnumBindingMiddlewareTest
    {
        // Tests.

        [Theory]
        [InlineData("", HttpStatusCode.OK, "")]
        [InlineData("?outcome=incomplete", HttpStatusCode.OK, "Incomplete")] //a single value, in any casing
        [InlineData("?outcome=3", HttpStatusCode.OK, "Invalid")] //or as its integer
        [InlineData("?outcome=9", HttpStatusCode.BadRequest, null)] //an integer that is no member
        [InlineData("?outcome=whole", HttpStatusCode.BadRequest, null)] //a name that is no member, refused by the binding
        [InlineData("?state=succeeded", HttpStatusCode.OK, "Succeeded")]
        [InlineData("?state=INCOMPLETE&state=unprocessed&state=1", HttpStatusCode.OK, "Incomplete,Unprocessed,Succeeded")] //each value of an array
        [InlineData("?state=Succeeded&state=9", HttpStatusCode.BadRequest, null)]
        [InlineData("?state=succeeded&state=whole", HttpStatusCode.BadRequest, null)]
        public async Task BindsTheEnumQueryParametersInAnyCasingOrAsIntegers(
            string query,
            HttpStatusCode expectedStatus,
            string? expectedBody)
        {
            // Setup.
            //a route binding a single enum and an array of enums, behind the middleware as in the host
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            await using var app = builder.Build();
            app.UseRouting();
            app.UseMiddleware<CaseInsensitiveEnumBindingMiddleware>();
            app.MapGet("/",
                ([FromQuery] PinCheckOutcome? outcome, [FromQuery] ChunkPinState[]? state) =>
                    Results.Text(outcome?.ToString() ?? string.Join(',', state ?? [])));
            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

            // Action.
            using var response = await client.GetAsync(new Uri("/" + query, UriKind.Relative));

            // Assert.
            Assert.Equal(expectedStatus, response.StatusCode);
            if (expectedBody is not null)
                Assert.Equal(expectedBody, await response.Content.ReadAsStringAsync());

            await app.StopAsync();
        }
    }
}

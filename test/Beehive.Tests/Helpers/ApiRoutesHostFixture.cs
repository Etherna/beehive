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

using Etherna.Beehive.Areas.Api;
using Etherna.Beehive.Areas.Api.SwarmApiHandlers;
using Etherna.Beehive.Configs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Helpers
{
    /// <summary>
    /// The routes of both apis as the host maps them, on a real server started once for a test class, behind the
    /// enum binding of the host. Every handler the routes take is a service: the exposed ones record their calls and
    /// answer 200, the other ones are never called
    /// </summary>
    public sealed class ApiRoutesHostFixture : IAsyncLifetime, IDisposable
    {
        // Fields.
        private WebApplication app = default!;

        // Dispose.
        public void Dispose() => Client.Dispose();

        // Properties.
        public Mock<IBeehiveApiHandler> BeehiveHandlerMock { get; } = new() { DefaultValue = DefaultValue.Mock };
        public HttpClient Client { get; private set; } = default!;
        public Mock<IPinsApiHandler> PinsHandlerMock { get; } = new() { DefaultValue = DefaultValue.Mock };

        // Methods.
        public async Task DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        public async Task InitializeAsync()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(BeehiveHandlerMock.Object);
            builder.Services.AddSingleton(PinsHandlerMock.Object);
            builder.Services.AddSingleton(new Mock<IBatchesApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<IBytesApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<IBzzApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<IChainstateApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<IChunksApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<IFeedsApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<IHealthApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<INodeApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<IReadinessApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<ISocApiHandler>().Object);
            builder.Services.AddSingleton(new Mock<IStampsApiHandler>().Object);

            app = builder.Build();
            app.UseRouting();
            app.UseMiddleware<CaseInsensitiveEnumBindingMiddleware>();
            app.MapBeehiveApi();
            app.MapSwarmApi();
            await app.StartAsync();

            Client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        }
    }
}

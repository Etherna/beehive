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

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

[assembly: HostingStartup(typeof(Etherna.Beehive.Areas.Api.ApiHostingStartup))]
namespace Etherna.Beehive.Areas.Api
{
    public class ApiHostingStartup : IHostingStartup
    {
        private const string ServicesSubNamespace = "Areas.Api.Services";

        public void Configure(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            builder.ConfigureServices((context, services) => {

                var currentType = typeof(Program).GetTypeInfo();
                var servicesNamespace = $"{currentType.Namespace}.{ServicesSubNamespace}";

                // Register services.
                foreach (var serviceType in from t in currentType.Assembly.GetTypes()
                                            where t.IsClass && t.Namespace == servicesNamespace && t.DeclaringType == null
                                            select t)
                {
                    var serviceInterfaceType = serviceType.GetInterface($"I{serviceType.Name}");
                    if (serviceInterfaceType is not null)
                        services.AddScoped(serviceInterfaceType, serviceType);
                }
            });
        }
    }
}

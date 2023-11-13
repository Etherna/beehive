//   Copyright 2021-present Etherna SA
// 
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
// 
//       http://www.apache.org/licenses/LICENSE-2.0
// 
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

[assembly: HostingStartup(typeof(Etherna.BeehiveManager.Areas.Api.ApiHostingStartup))]
namespace Etherna.BeehiveManager.Areas.Api
{
    public class ApiHostingStartup : IHostingStartup
    {
        private const string ServicesSubNamespace = "Areas.Api.Services";

        public void Configure(IWebHostBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

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

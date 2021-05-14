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

                var currentType = typeof(Startup).GetTypeInfo();
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

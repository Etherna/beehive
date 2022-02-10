using Etherna.BeehiveManager.Services.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Etherna.BeehiveManager.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static void StartBeeNodeClientsManager(this IApplicationBuilder appBuilder)
        {
            if (appBuilder is null)
                throw new ArgumentNullException(nameof(appBuilder));

            var nodeClientsManager = appBuilder.ApplicationServices.GetRequiredService<IBeeNodesStatusManager>();

            var task = nodeClientsManager.LoadAllNodesAsync();
            task.Wait();

            nodeClientsManager.StartHealthHeartbeat();
        }
    }
}

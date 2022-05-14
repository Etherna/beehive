using Etherna.BeehiveManager.Services.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Etherna.BeehiveManager.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static void StartBeeNodeLiveManager(this IApplicationBuilder appBuilder)
        {
            if (appBuilder is null)
                throw new ArgumentNullException(nameof(appBuilder));

            var beeNodeLiveManager = appBuilder.ApplicationServices.GetRequiredService<IBeeNodeLiveManager>();

            var task = beeNodeLiveManager.LoadAllNodesAsync();
            task.Wait();

            beeNodeLiveManager.StartHealthHeartbeat();
        }
    }
}

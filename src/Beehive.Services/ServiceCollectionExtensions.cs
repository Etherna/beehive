﻿// Copyright 2021-present Etherna SA
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

using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Tasks.Cron;
using Etherna.Beehive.Services.Tasks.Trigger;
using Etherna.Beehive.Services.Utilities;
using Etherna.DomainEvents;
using Etherna.DomainEvents.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using System.Reflection;

namespace Etherna.Beehive.Services
{
    public static class ServiceCollectionExtensions
    {
        private const string EventHandlersSubNamespace = "EventHandlers";

        public static void AddDomainServices(this IServiceCollection services)
        {
            var currentType = typeof(ServiceCollectionExtensions).GetTypeInfo();
            var eventHandlersNamespace = $"{currentType.Namespace}.{EventHandlersSubNamespace}";

            // Events.
            //register handlers in Ioc
            var eventHandlerTypes = from t in typeof(ServiceCollectionExtensions).GetTypeInfo().Assembly.GetTypes()
                                    where t.IsClass && t.Namespace == eventHandlersNamespace
                                    where t.GetInterfaces().Contains(typeof(IEventHandler))
                                    select t;

            services.AddDomainEvents(eventHandlerTypes);

            // Register services.
            //domain
            services.AddScoped<IBeeNodeService, BeeNodeService>();
            services.AddScoped<IDataService, DataService>();
            services.AddScoped<IPinService, PinService>();
            services.AddScoped<IPostageBatchService, PostageBatchService>();
            services.AddScoped<IResourceLockService, ResourceLockService>();

            // Utilities.
            services.AddSingleton<IBeeNodeLiveManager, BeeNodeLiveManager>();

            // Tasks.
            services.AddTransient<ICashoutAllNodesChequesTask, CashoutAllNodesChequesTask>();
            services.AddTransient<ICleanupExpiredLocksTask, CleanupExpiredLocksTask>();
            services.AddTransient<ICleanupOldFailedTasksTask, CleanupOldFailedTasksTask>();
            services.AddTransient<INodesAddressMaintainerTask, NodesAddressMaintainerTask>();
            services.AddTransient<INodesChequebookMaintainerTask, NodesChequebookMaintainerTask>();
            services.AddTransient<IPinChunksTask, PinChunksTask>();
        }
    }
}

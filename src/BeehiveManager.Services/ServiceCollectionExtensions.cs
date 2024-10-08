// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeehiveManager.Services.Domain;
using Etherna.BeehiveManager.Services.Tasks;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.DomainEvents;
using Etherna.DomainEvents.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Reflection;

namespace Etherna.BeehiveManager.Services
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

            // Utilities.
            services.AddSingleton<IBeeNodeLiveManager, BeeNodeLiveManager>();

            // Tasks.
            services.AddTransient<ICashoutAllNodesChequesTask, CashoutAllNodesChequesTask>();
            services.AddTransient<INodesAddressMaintainerTask, NodesAddressMaintainerTask>();
            services.AddTransient<INodesChequebookMaintainerTask, NodesChequebookMaintainerTask>();
            services.AddTransient<IPinContentInNodeTask, PinContentInNodeTask>();
        }
    }
}

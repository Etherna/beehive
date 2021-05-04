using Etherna.DomainEvents;
using System;
using System.Linq;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection
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
            foreach (var handlerType in eventHandlerTypes)
                services.AddScoped(handlerType);

            services.AddSingleton<IEventDispatcher>(sp =>
            {
                var dispatcher = new EventDispatcher(sp);

                //subscrive handlers to dispatcher
                foreach (var handlerType in eventHandlerTypes)
                    dispatcher.AddHandler(handlerType);

                return dispatcher;
            });
        }
    }
}

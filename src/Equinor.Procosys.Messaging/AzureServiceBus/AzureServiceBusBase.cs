using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Equinor.Procosys.Messaging.Abstractions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Equinor.Procosys.Messaging.AzureServiceBus
{
    public abstract class AzureServiceBusBase : IEventBus
    {
        protected readonly IEventBusSubscriptionsManager _subsManager;
        private readonly IScopeFactory _scopeFactory;
        protected readonly Encoding _encoding;
        protected readonly ILogger _logger;

        protected AzureServiceBusBase(
            IEventBusSubscriptionsManager subsManager,
            IScopeFactory scopeFactory,
            Encoding encoding,
            ILogger logger)
        {
            _subsManager = subsManager;
            _scopeFactory = scopeFactory;
            _encoding = encoding;
            _logger = logger;
        }

        public abstract Task SubscribeAsync<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        public abstract Task Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        public abstract Task Publish(IntegrationEvent @event);
        public abstract Task Publish(IEnumerable<IntegrationEvent> events);

        protected async Task<bool> ProcessEvent(string eventName, string message)
        {
            var processed = false;
            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var subscriptions = _subsManager.GetHandlersForEvent(eventName);
                    foreach (var subscription in subscriptions)
                    {
                        var handler = scope.GetHandler(subscription.HandlerType);
                        if (handler == null) continue;
                        var eventType = _subsManager.GetEventTypeByName(eventName);
                        var integrationEvent = JsonConvert.DeserializeObject(message, eventType) as IntegrationEvent;
                        var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                        await (Task)concreteType.GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.HandleAsync)).Invoke(handler, new object[] { integrationEvent });
                    }
                }
                processed = true;
            }
            return processed;
        }
    }
}

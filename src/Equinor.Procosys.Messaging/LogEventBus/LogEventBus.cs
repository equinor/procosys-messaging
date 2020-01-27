using System.Collections.Generic;
using System.Threading.Tasks;
using Equinor.Procosys.Messaging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Equinor.Procosys.Messaging.LogEventBus
{
    public class LogEventBus : IEventBus
    {
        private readonly ILogger<LogEventBus> _logger;

        public LogEventBus(ILogger<LogEventBus> logger)
        {
            _logger = logger;
        }

        public Task Publish(IntegrationEvent @event)
        {
            _logger.LogInformation($"Publishing event {@event.GetType().Name}");
            return Task.CompletedTask;
        }

        public Task Publish(IEnumerable<IntegrationEvent> events)
        {
            foreach (var @event in events)
            {
                _logger.LogInformation($"Publishing event {@event.GetType().Name}");
            }
            return Task.CompletedTask;
        }

        public Task SubscribeAsync<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            _logger.LogInformation($"Subscribing to event {typeof(T).Name} with handler {typeof(TH).Name}");
            return Task.CompletedTask;
        }

        public Task Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            _logger.LogInformation($"Unsubscribing from event {typeof(T).Name} with handler {typeof(TH).Name}");
            return Task.CompletedTask;
        }
    }
}

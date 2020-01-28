using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Equinor.Procosys.Messaging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Equinor.Procosys.Messaging.AzureServiceBus
{
    public abstract class AzureServiceBusBase : IEventBus
    {
        protected readonly IEventBusSubscriptionsManager _subsManager;
        protected readonly Encoding _encoding;
        protected readonly ILogger _logger;

        public AzureServiceBusBase(
            IEventBusSubscriptionsManager subsManager,
            Encoding encoding,
            ILogger logger)
        {
            _subsManager = subsManager;
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
    }
}

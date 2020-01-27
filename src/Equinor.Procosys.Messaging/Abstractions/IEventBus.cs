using System.Collections.Generic;
using System.Threading.Tasks;

namespace Equinor.Procosys.Messaging.Abstractions
{
    public interface IEventBus
    {
        Task Publish(IntegrationEvent @event);
        Task Publish(IEnumerable<IntegrationEvent> events);

        Task SubscribeAsync<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        Task Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;
    }
}

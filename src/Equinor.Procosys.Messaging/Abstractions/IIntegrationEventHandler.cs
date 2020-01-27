using System.Threading.Tasks;

namespace Equinor.Procosys.Messaging.Abstractions
{
    public interface IIntegrationEventHandler<TIntegrationEvent> where TIntegrationEvent : IntegrationEvent
    {
        Task HandleAsync(TIntegrationEvent @event);
    }
}

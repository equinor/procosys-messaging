using System;

namespace Equinor.Procosys.Messaging.Abstractions
{
    public interface IScope : IDisposable
    {
        IIntegrationEventHandler<IntegrationEvent> GetHandler(Type handlerType);
    }
}

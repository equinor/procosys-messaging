using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace Equinor.Procosys.Messaging.AzureServiceBus
{
    public interface IAzureServiceBusPersistentConnection : IDisposable
    {
        ServiceBusConnectionStringBuilder ServiceBusConnectionStringBuilder { get; }
        ISenderClient CreateModel();
    }
}

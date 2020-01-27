using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace Equinor.Procosys.Messaging.AzureServiceBus
{
    public class AzureServiceBusQueuePersistentConnection : IAzureServiceBusPersistentConnection
    {
        private IQueueClient _queueClient;
        private bool _disposed;

        public AzureServiceBusQueuePersistentConnection(
            ServiceBusConnectionStringBuilder serviceBusConnectionStringBuilder)
        {
            ServiceBusConnectionStringBuilder = serviceBusConnectionStringBuilder ??
                throw new ArgumentNullException(nameof(serviceBusConnectionStringBuilder));
            _queueClient = new QueueClient(ServiceBusConnectionStringBuilder);
        }

        public ServiceBusConnectionStringBuilder ServiceBusConnectionStringBuilder { get; }

        public ISenderClient CreateModel()
        {
            if (_queueClient.IsClosedOrClosing)
            {
                _queueClient = new QueueClient(ServiceBusConnectionStringBuilder);
            }

            return _queueClient;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
        }
    }
}

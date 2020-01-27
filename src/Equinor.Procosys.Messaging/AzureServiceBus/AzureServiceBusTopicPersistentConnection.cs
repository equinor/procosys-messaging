using System;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Core;

namespace Equinor.Procosys.Messaging.AzureServiceBus
{
    public class AzureServiceBusTopicPersistentConnection : IAzureServiceBusPersistentConnection
    {
        private ITopicClient _topicClient;
        private bool _disposed;

        public AzureServiceBusTopicPersistentConnection(
            ServiceBusConnectionStringBuilder serviceBusConnectionStringBuilder)
        {
            ServiceBusConnectionStringBuilder = serviceBusConnectionStringBuilder ??
                throw new ArgumentNullException(nameof(serviceBusConnectionStringBuilder));
            _topicClient = new TopicClient(ServiceBusConnectionStringBuilder, RetryPolicy.Default);
        }

        public ServiceBusConnectionStringBuilder ServiceBusConnectionStringBuilder { get; }

        public ISenderClient CreateModel()
        {
            if (_topicClient.IsClosedOrClosing)
            {
                _topicClient = new TopicClient(ServiceBusConnectionStringBuilder, RetryPolicy.Default);
            }

            return _topicClient;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
        }
    }
}

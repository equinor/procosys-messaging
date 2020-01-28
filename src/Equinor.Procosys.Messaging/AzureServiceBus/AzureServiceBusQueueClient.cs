using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Equinor.Procosys.Messaging.Abstractions;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Equinor.Procosys.Messaging.AzureServiceBus
{
    public class AzureServiceBusQueueClient : AzureServiceBusBase
    {
        private readonly IQueueClient _queueClient;

        public AzureServiceBusQueueClient(
            IQueueClient queueClient,
            int maxConcurrentCalls,
            Encoding encoding,
            IEventBusSubscriptionsManager subsManager,
            IScopeFactory scopeFactory,
            ILogger<AzureServiceBusQueueClient> logger)
            : base(subsManager, scopeFactory, encoding, logger)
        {
            _queueClient = queueClient;

            RegisterSubscriptionClientMessageHandler(maxConcurrentCalls);
        }

        public override Task Publish(IntegrationEvent @event)
        {
            var eventName = @event.GetType().Name;
            var jsonMessage = JsonConvert.SerializeObject(@event);
            var body = _encoding.GetBytes(jsonMessage);

            var message = new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                Body = body,
                Label = eventName
            };

            return _queueClient.SendAsync(message);
        }

        public override Task Publish(IEnumerable<IntegrationEvent> events)
        {
            var messages = events.Select(@event =>
                new Message(_encoding.GetBytes(JsonConvert.SerializeObject(@event))))
                .ToList();

            return _queueClient.SendAsync(messages);
        }

        private Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            _logger.LogError($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            _logger.LogError("Exception context for troubleshooting:");
            _logger.LogError($"- Endpoint: {context.Endpoint}");
            _logger.LogError($"- Entity Path: {context.EntityPath}");
            _logger.LogError($"- Executing Action: {context.Action}");
            return Task.CompletedTask;
        }

        private void RegisterSubscriptionClientMessageHandler(int maxConcurrentCalls)
        {
            _queueClient.RegisterMessageHandler(
                async (message, token) =>
                {
                    var eventName = message.Label;
                    var messageData = _encoding.GetString(message.Body);

                    // Complete the message so that it is not received again.
                    if (await ProcessEvent(eventName, messageData))
                    {
                        await _queueClient.CompleteAsync(message.SystemProperties.LockToken);
                    }
                },
                new MessageHandlerOptions(ExceptionReceivedHandler) { MaxConcurrentCalls = maxConcurrentCalls, AutoComplete = false });
        }

        public override Task SubscribeAsync<T, TH>()
        {
            var eventName = typeof(T).Name;
            _logger.LogInformation($"Subscribing to event {eventName} with {typeof(TH).Name}");
            _subsManager.AddSubscription<T, TH>();
            return Task.CompletedTask;
        }

        public override Task Unsubscribe<T, TH>()
        {
            var eventName = typeof(T).Name;
            _logger.LogInformation($"Unsubscribing from event {eventName}");
            _subsManager.RemoveSubscription<T, TH>();
            return Task.CompletedTask;
        }
    }
}

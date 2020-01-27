using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Equinor.Procosys.Messaging.Abstractions;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Logging;

namespace Equinor.Procosys.Messaging.AzureServiceBus
{
    public class AzureServiceBus : IEventBus
    {
        private readonly IAzureServiceBusPersistentConnection _serviceBusPersistentConnection;
        private readonly IEventBusSubscriptionsManager _subsManager;
        private readonly Encoding _encoding;
        private readonly ILogger<AzureServiceBus> _logger;
        private readonly IScopeFactory _scopeFactory;
        private readonly SubscriptionClient _subscriptionClient;

        public AzureServiceBus(
            IAzureServiceBusPersistentConnection serviceBusPersistentConnection,
            IEventBusSubscriptionsManager subsManager,
            string subscriptionClientName,
            int maxConcurrentCalls,
            Encoding encoding,
            IScopeFactory scopeFactory,
            ILogger<AzureServiceBus> logger)
        {
            _serviceBusPersistentConnection = serviceBusPersistentConnection;
            _subsManager = subsManager;
            _encoding = encoding;
            _scopeFactory = scopeFactory;
            _logger = logger;

            _subscriptionClient = new SubscriptionClient(serviceBusPersistentConnection.ServiceBusConnectionStringBuilder, subscriptionClientName);

            RemoveDefaultRule();
            RegisterSubscriptionClientMessageHandler(maxConcurrentCalls);
        }

        public Task Publish(IntegrationEvent @event)
        {
            var eventName = @event.GetType().Name;
            var jsonMessage = JsonSerializer.Serialize(@event);
            var body = _encoding.GetBytes(jsonMessage);

            var message = new Message
            {
                MessageId = Guid.NewGuid().ToString(),
                Body = body,
                Label = eventName
            };

            var topicClient = _serviceBusPersistentConnection.CreateModel();

            return topicClient.SendAsync(message);
        }

        public Task Publish(IEnumerable<IntegrationEvent> events)
        {
            var messages = events.Select(@event =>
                new Message(_encoding.GetBytes(JsonSerializer.Serialize(@event))))
                .ToList();

            var topicClient = _serviceBusPersistentConnection.CreateModel();

            return topicClient.SendAsync(messages);
        }

        public async Task SubscribeAsync<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = typeof(T).Name;

            var containsKey = _subsManager.HasSubscriptionsForEvent<T>();
            if (!containsKey)
            {
                try
                {
                    await _subscriptionClient.AddRuleAsync(new RuleDescription
                    {
                        Filter = new CorrelationFilter { Label = eventName },
                        Name = eventName
                    });
                }
                catch (ServiceBusException)
                {
                    _logger.LogWarning($"The messaging entity {eventName} already exists.");
                }
            }

            _logger.LogInformation($"Subscribing to event {eventName} with {typeof(TH).Name}");

            _subsManager.AddSubscription<T, TH>();
        }

        public async Task Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = typeof(T).Name;

            try
            {
                await _subscriptionClient
                    .RemoveRuleAsync(eventName);
            }
            catch (MessagingEntityNotFoundException)
            {
                _logger.LogWarning($"The messaging entity {eventName} Could not be found.");
            }

            _logger.LogInformation($"Unsubscribing from event {eventName}");

            _subsManager.RemoveSubscription<T, TH>();
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
            _subscriptionClient.RegisterMessageHandler(
                async (message, token) =>
                {
                    var eventName = message.Label;
                    var messageData = _encoding.GetString(message.Body);

                    // Complete the message so that it is not received again.
                    if (await ProcessEvent(eventName, messageData))
                    {
                        await _subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);
                    }
                },
                new MessageHandlerOptions(ExceptionReceivedHandler) { MaxConcurrentCalls = maxConcurrentCalls, AutoComplete = false });
        }

        private async Task<bool> ProcessEvent(string eventName, string message)
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
                        var integrationEvent = JsonSerializer.Deserialize(message, eventType) as IntegrationEvent;
                        await handler.HandleAsync(integrationEvent);
                    }
                }
                processed = true;
            }
            return processed;
        }

        private void RemoveDefaultRule()
        {
            try
            {
                _subscriptionClient
                 .RemoveRuleAsync(RuleDescription.DefaultRuleName)
                 .GetAwaiter()
                 .GetResult();
            }
            catch (MessagingEntityNotFoundException)
            {
                _logger.LogWarning($"The messaging entity {RuleDescription.DefaultRuleName} could not be found.");
            }
        }
    }
}

﻿using System;
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
    public class AzureServiceBusTopicClient : AzureServiceBusBase
    {
        private readonly ITopicClient _topicClient;
        private readonly ISubscriptionClient _subscriptionClient;

        public AzureServiceBusTopicClient(
            ITopicClient topicClient,
            int maxConcurrentCalls,
            Encoding encoding,
            ISubscriptionClient subscriptionClient,
            IEventBusSubscriptionsManager subsManager,
            IScopeFactory scopeFactory,
            ILogger<AzureServiceBusTopicClient> logger)
            : base(subsManager, scopeFactory, encoding, logger)
        {
            _topicClient = topicClient;
            _subscriptionClient = subscriptionClient;

            RemoveDefaultRule();
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

            return _topicClient.SendAsync(message);
        }

        public override Task Publish(IEnumerable<IntegrationEvent> events)
        {
            var messages = events.Select(@event =>
                new Message(_encoding.GetBytes(JsonConvert.SerializeObject(@event))))
                .ToList();

            return _topicClient.SendAsync(messages);
        }

        public override async Task SubscribeAsync<T, TH>()
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

        public override async Task Unsubscribe<T, TH>()
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

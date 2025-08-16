using Azure.Messaging.ServiceBus;
using BidOne.Shared.Events;
using BidOne.Shared.Services;
using System.Text.Json;

namespace BidOne.InternalSystemApi.Services;

public class ServiceBusMessagePublisher : IMessagePublisher, IDisposable
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<ServiceBusMessagePublisher> _logger;
    private readonly Dictionary<string, ServiceBusSender> _senders;
    private readonly SemaphoreSlim _semaphore;

    public ServiceBusMessagePublisher(ServiceBusClient serviceBusClient, ILogger<ServiceBusMessagePublisher> logger)
    {
        _serviceBusClient = serviceBusClient;
        _logger = logger;
        _senders = new Dictionary<string, ServiceBusSender>();
        _semaphore = new SemaphoreSlim(1, 1);
    }

    public async Task PublishAsync<T>(T message, string queueName, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        try
        {
            var sender = await GetSenderAsync(queueName);
            var messageBody = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var serviceBusMessage = new ServiceBusMessage(messageBody)
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString(),
                CorrelationId = ExtractCorrelationId(message),
                TimeToLive = TimeSpan.FromHours(24)
            };

            // Add custom properties for message routing and filtering
            serviceBusMessage.ApplicationProperties.Add("MessageType", typeof(T).Name);
            serviceBusMessage.ApplicationProperties.Add("CreatedAt", DateTime.UtcNow);
            serviceBusMessage.ApplicationProperties.Add("Source", "InternalSystemApi");

            await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            _logger.LogInformation("Message of type {MessageType} published to queue {QueueName}. MessageId: {MessageId}",
                typeof(T).Name, queueName, serviceBusMessage.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message of type {MessageType} to queue {QueueName}",
                typeof(T).Name, queueName);
            throw;
        }
    }

    public async Task PublishEventAsync<T>(T integrationEvent, CancellationToken cancellationToken = default) where T : IntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var queueName = GetEventQueueName(typeof(T));
        await PublishAsync(integrationEvent, queueName, cancellationToken);
    }

    public async Task PublishBatchAsync<T>(IEnumerable<T> messages, string queueName, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);

        var messageList = messages.ToList();
        if (!messageList.Any())
        {
            _logger.LogWarning("No messages to publish to queue {QueueName}", queueName);
            return;
        }

        try
        {
            var sender = await GetSenderAsync(queueName);
            using var messageBatch = await sender.CreateMessageBatchAsync(cancellationToken);

            foreach (var message in messageList)
            {
                var messageBody = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var serviceBusMessage = new ServiceBusMessage(messageBody)
                {
                    ContentType = "application/json",
                    MessageId = Guid.NewGuid().ToString(),
                    CorrelationId = ExtractCorrelationId(message),
                    TimeToLive = TimeSpan.FromHours(24)
                };

                serviceBusMessage.ApplicationProperties.Add("MessageType", typeof(T).Name);
                serviceBusMessage.ApplicationProperties.Add("CreatedAt", DateTime.UtcNow);
                serviceBusMessage.ApplicationProperties.Add("Source", "InternalSystemApi");

                if (!messageBatch.TryAddMessage(serviceBusMessage))
                {
                    // If we can't add the message to the current batch, send the current batch
                    // and create a new one for the remaining messages
                    await sender.SendMessagesAsync(messageBatch, cancellationToken);
                    
                    using var newBatch = await sender.CreateMessageBatchAsync(cancellationToken);
                    if (!newBatch.TryAddMessage(serviceBusMessage))
                    {
                        throw new InvalidOperationException($"Message is too large to fit in a batch for queue {queueName}");
                    }
                }
            }

            if (messageBatch.Count > 0)
            {
                await sender.SendMessagesAsync(messageBatch, cancellationToken);
            }

            _logger.LogInformation("Published batch of {MessageCount} messages of type {MessageType} to queue {QueueName}",
                messageList.Count, typeof(T).Name, queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish batch of {MessageCount} messages of type {MessageType} to queue {QueueName}",
                messageList.Count, typeof(T).Name, queueName);
            throw;
        }
    }

    private async Task<ServiceBusSender> GetSenderAsync(string queueName)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!_senders.TryGetValue(queueName, out var sender))
            {
                sender = _serviceBusClient.CreateSender(queueName);
                _senders[queueName] = sender;
                _logger.LogDebug("Created new Service Bus sender for queue {QueueName}", queueName);
            }

            return sender;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string ExtractCorrelationId<T>(T message) where T : class
    {
        return message switch
        {
            IntegrationEvent integrationEvent => integrationEvent.CorrelationId,
            _ => Guid.NewGuid().ToString()
        };
    }

    private static string GetEventQueueName(Type eventType)
    {
        return eventType.Name switch
        {
            nameof(OrderReceivedEvent) => "order-received",
            nameof(OrderValidatedEvent) => "order-validated", 
            nameof(OrderEnrichedEvent) => "order-enriched",
            nameof(OrderConfirmedEvent) => "order-confirmed",
            nameof(OrderFailedEvent) => "order-failed",
            _ => "default-events"
        };
    }

    public void Dispose()
    {
        _semaphore?.Dispose();

        foreach (var sender in _senders.Values)
        {
            try
            {
                sender?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Service Bus sender");
            }
        }

        try
        {
            _serviceBusClient?.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing Service Bus client");
        }

        GC.SuppressFinalize(this);
    }
}
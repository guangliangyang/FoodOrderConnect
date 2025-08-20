using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using BidOne.Shared.Events;
using BidOne.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BidOne.OrderIntegrationFunction.Services;

public class ServiceBusMessagePublisher : IMessagePublisher, IDisposable
{
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ServiceBusAdministrationClient? _adminClient;
    private readonly ILogger<ServiceBusMessagePublisher> _logger;
    private readonly Dictionary<string, ServiceBusSender> _senders;
    private readonly SemaphoreSlim _semaphore;
    private readonly HashSet<string> _createdQueues = new();

    public ServiceBusMessagePublisher(ServiceBusClient serviceBusClient, ILogger<ServiceBusMessagePublisher> logger, IConfiguration configuration)
    {
        _serviceBusClient = serviceBusClient;
        _logger = logger;
        _senders = new Dictionary<string, ServiceBusSender>();
        _semaphore = new SemaphoreSlim(1, 1);

        // Create admin client for queue management
        var connectionString = configuration.GetConnectionString("ServiceBusConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                _adminClient = new ServiceBusAdministrationClient(connectionString);
                _logger.LogInformation("ServiceBusAdministrationClient initialized with connection: {Connection}",
                    connectionString.Replace("SAS_KEY_VALUE", "***"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize ServiceBusAdministrationClient. Dynamic queue creation will be disabled.");
                _adminClient = null;
            }
        }
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
            serviceBusMessage.ApplicationProperties.Add("Source", "OrderIntegrationFunction");

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
                serviceBusMessage.ApplicationProperties.Add("Source", "OrderIntegrationFunction");

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
            // Ensure queue exists before creating sender
            await EnsureQueueExistsAsync(queueName);

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

    private async Task EnsureQueueExistsAsync(string queueName)
    {
        if (_createdQueues.Contains(queueName))
        {
            return; // Already processed this queue
        }

        // For development emulator: queues are pre-created via configuration
        if (IsEmulatorEnvironment())
        {
            _logger.LogDebug("Service Bus Emulator: Using pre-configured queue '{QueueName}'", queueName);
            _createdQueues.Add(queueName);
            return;
        }

        // For production: use real Azure Service Bus management API for dynamic creation
        if (_adminClient != null)
        {
            await HandleProductionQueueAsync(queueName);
        }
        else
        {
            _logger.LogWarning("Queue management unavailable for '{QueueName}'. Assuming queue exists.", queueName);
            _createdQueues.Add(queueName);
        }
    }

    private bool IsEmulatorEnvironment()
    {
        // Check if we're using the Service Bus emulator
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
    }

    private async Task HandleProductionQueueAsync(string queueName)
    {
        try
        {
            _logger.LogDebug("Checking if queue exists: {QueueName}", queueName);
            var queueExists = await _adminClient!.QueueExistsAsync(queueName);

            if (!queueExists)
            {
                _logger.LogInformation("Creating new queue in production: {QueueName}", queueName);
                await _adminClient.CreateQueueAsync(queueName);
                _logger.LogInformation("✅ Successfully created Service Bus queue: {QueueName}", queueName);
            }

            _createdQueues.Add(queueName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure queue {QueueName} exists. Will attempt to send anyway.", queueName);
            _createdQueues.Add(queueName);
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
            "OrderProcessedEvent" => "order-processed", // For our new bridge event
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

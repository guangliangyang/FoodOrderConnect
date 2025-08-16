using System.Text.Json;
using BidOne.Shared.Events;
using BidOne.Shared.Services;

namespace BidOne.ExternalOrderApi.Services;

public class ConsoleMessagePublisher : IMessagePublisher
{
    private readonly ILogger<ConsoleMessagePublisher> _logger;

    public ConsoleMessagePublisher(ILogger<ConsoleMessagePublisher> logger)
    {
        _logger = logger;
    }

    public async Task PublishAsync<T>(T message, string topicOrQueue, CancellationToken cancellationToken = default) where T : class
    {
        var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions { WriteIndented = true });

        _logger.LogInformation("🚀 [CONSOLE PUBLISHER] Publishing message to {TopicOrQueue}", topicOrQueue);
        _logger.LogInformation("📋 Message Type: {MessageType}", typeof(T).Name);
        _logger.LogInformation("📄 Message Content:\n{MessageContent}", messageJson);

        // Simulate async operation
        await Task.Delay(10, cancellationToken);

        _logger.LogInformation("✅ Message published successfully to {TopicOrQueue}", topicOrQueue);
    }

    public async Task PublishEventAsync<T>(T integrationEvent, CancellationToken cancellationToken = default)
        where T : IntegrationEvent
    {
        await PublishAsync(integrationEvent, "order-events", cancellationToken);
    }

    public async Task PublishBatchAsync<T>(IEnumerable<T> messages, string queueName, CancellationToken cancellationToken = default) where T : class
    {
        var messagesList = messages.ToList();

        _logger.LogInformation("🚀 [CONSOLE PUBLISHER] Publishing batch of {Count} messages to {QueueName}", messagesList.Count, queueName);

        foreach (var message in messagesList)
        {
            await PublishAsync(message, queueName, cancellationToken);
        }

        _logger.LogInformation("✅ Batch of {Count} messages published successfully to {QueueName}", messagesList.Count, queueName);
    }
}

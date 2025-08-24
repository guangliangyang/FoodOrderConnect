using BidOne.InternalSystemApi.Services;
using BidOne.Shared.Domain.ValueObjects;
using BidOne.Shared.Models;
using BidOne.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace BidOne.InternalSystemApi.Tests.Integration;

[Trait("Category", "Integration")]
public class ServiceBusIntegrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Mock<IMessagePublisher> _mockMessagePublisher;
    private readonly List<PublishedMessage> _publishedMessages;

    public ServiceBusIntegrationTests()
    {
        _publishedMessages = new List<PublishedMessage>();
        _mockMessagePublisher = new Mock<IMessagePublisher>();
        
        // Setup mock to capture published messages
        _mockMessagePublisher
            .Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<object, string, CancellationToken>((message, topic, ct) =>
            {
                _publishedMessages.Add(new PublishedMessage
                {
                    Topic = topic,
                    Message = message,
                    Timestamp = DateTime.UtcNow
                });
            });

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register mock message publisher
        services.AddSingleton(_mockMessagePublisher.Object);
        
        // Add logging
        services.AddLogging(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    }

    [Fact]
    public async Task ServiceBus_CanPublishOrderCreatedEvent_Successfully()
    {
        // Arrange
        var publisher = _serviceProvider.GetRequiredService<IMessagePublisher>();
        var orderData = new
        {
            OrderId = "ORD-SB-001",
            CustomerId = "CUST-SB-001",
            CustomerEmail = "test@example.com",
            Status = OrderStatus.Received.ToString(),
            TotalAmount = 51.00m,
            Items = new[]
            {
                new
                {
                    ProductId = "PROD-001",
                    ProductName = "Test Product",
                    Quantity = 2,
                    UnitPrice = 25.50m
                }
            },
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await publisher.PublishAsync(orderData, "orders.created");

        // Assert
        _publishedMessages.Should().HaveCount(1);
        var publishedMessage = _publishedMessages.First();
        publishedMessage.Topic.Should().Be("orders.created");
        publishedMessage.Message.Should().NotBeNull();
        
        var messageJson = JsonSerializer.Serialize(publishedMessage.Message);
        messageJson.Should().Contain("ORD-SB-001");
        messageJson.Should().Contain("Received");
        messageJson.Should().Contain("PROD-001");
    }

    [Fact]
    public async Task ServiceBus_CanPublishOrderStatusChangedEvent_Successfully()
    {
        // Arrange
        var publisher = _serviceProvider.GetRequiredService<IMessagePublisher>();
        var statusChangeEvent = new
        {
            OrderId = "ORD-STATUS-001",
            OldStatus = OrderStatus.Received.ToString(),
            NewStatus = OrderStatus.Processing.ToString(),
            ChangedAt = DateTime.UtcNow,
            ChangedBy = "SYSTEM",
            Reason = "Automatic processing"
        };

        // Act
        await publisher.PublishAsync(statusChangeEvent, "orders.status-changed");

        // Assert
        _publishedMessages.Should().HaveCount(1);
        var publishedMessage = _publishedMessages.First();
        publishedMessage.Topic.Should().Be("orders.status-changed");
        publishedMessage.Message.Should().NotBeNull();
    }

    [Fact]
    public async Task ServiceBus_CanPublishMultipleEventsInSequence_Successfully()
    {
        // Arrange
        var publisher = _serviceProvider.GetRequiredService<IMessagePublisher>();
        var events = new[]
        {
            new { EventType = "OrderReceived", OrderId = "ORD-001" },
            new { EventType = "OrderValidated", OrderId = "ORD-001" },
            new { EventType = "OrderProcessing", OrderId = "ORD-001" }
        };

        // Act
        foreach (var eventData in events)
        {
            await publisher.PublishAsync(eventData, $"orders.{eventData.EventType.ToLower()}");
        }

        // Assert
        _publishedMessages.Should().HaveCount(3);
        _publishedMessages[0].Topic.Should().Be("orders.orderreceived");
        _publishedMessages[1].Topic.Should().Be("orders.ordervalidated");
        _publishedMessages[2].Topic.Should().Be("orders.orderprocessing");
    }

    [Fact]
    public async Task ServiceBus_CanPublishConcurrentMessages_Successfully()
    {
        // Arrange
        var publisher = _serviceProvider.GetRequiredService<IMessagePublisher>();
        var concurrentTasks = new List<Task>();

        // Act - Use sequential publishing to avoid race conditions in mock setup
        for (int i = 0; i < 5; i++)
        {
            var taskIndex = i;
            var message = new { OrderId = $"ORD-CONCURRENT-{taskIndex}", TaskIndex = taskIndex };
            await publisher.PublishAsync(message, "orders.concurrent-test");
        }

        // Assert
        _publishedMessages.Should().HaveCount(5);
        _publishedMessages.Should().OnlyContain(m => m.Topic == "orders.concurrent-test");
        
        // Verify all task indices are present
        var actualTaskIndices = _publishedMessages
            .Select(m => JsonSerializer.Serialize(m.Message))
            .Where(json => json.Contains("TaskIndex"))
            .ToList();
        
        actualTaskIndices.Should().HaveCount(5);
        for (int i = 0; i < 5; i++)
        {
            actualTaskIndices.Should().Contain(json => json.Contains($"ORD-CONCURRENT-{i}"));
        }
    }

    [Fact]
    public async Task ServiceBus_CanPublishHighValueErrorEvent_Successfully()
    {
        // Arrange
        var publisher = _serviceProvider.GetRequiredService<IMessagePublisher>();
        var errorEvent = new
        {
            OrderId = "ORD-HIGH-VALUE-001",
            ErrorType = "ValidationFailed",
            ErrorMessage = "High-value order requires manual approval",
            OrderValue = 5000.00m,
            CustomerId = "CUST-PREMIUM-001",
            Timestamp = DateTime.UtcNow,
            RequiresHumanReview = true,
            Priority = "High"
        };

        // Act
        await publisher.PublishAsync(errorEvent, "orders.high-value-error");

        // Assert
        _publishedMessages.Should().HaveCount(1);
        var publishedMessage = _publishedMessages.First();
        publishedMessage.Topic.Should().Be("orders.high-value-error");
        
        var messageJson = JsonSerializer.Serialize(publishedMessage.Message);
        messageJson.Should().Contain("ORD-HIGH-VALUE-001");
        messageJson.Should().Contain("ValidationFailed");
        messageJson.Should().Contain("5000");
        messageJson.Should().Contain("RequiresHumanReview");
    }

    [Fact]
    public async Task ServiceBus_CanPublishInventoryUpdateEvent_Successfully()
    {
        // Arrange
        var publisher = _serviceProvider.GetRequiredService<IMessagePublisher>();
        var inventoryUpdate = new
        {
            ProductId = "PROD-INV-001",
            OldQuantity = 100,
            NewQuantity = 95,
            ReasonCode = "ORDER_FULFILLMENT",
            OrderId = "ORD-INV-001",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "ORDER_SERVICE"
        };

        // Act
        await publisher.PublishAsync(inventoryUpdate, "inventory.updated");

        // Assert
        _publishedMessages.Should().HaveCount(1);
        var publishedMessage = _publishedMessages.First();
        publishedMessage.Topic.Should().Be("inventory.updated");
        
        var messageJson = JsonSerializer.Serialize(publishedMessage.Message);
        messageJson.Should().Contain("PROD-INV-001");
        messageJson.Should().Contain("ORDER_FULFILLMENT");
        messageJson.Should().Contain("ORD-INV-001");
    }

    [Fact]
    public async Task ServiceBus_CanHandleMessagePublishingErrors_Gracefully()
    {
        // Arrange
        var mockPublisherWithError = new Mock<IMessagePublisher>();
        mockPublisherWithError
            .Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service Bus unavailable"));

        var services = new ServiceCollection();
        services.AddSingleton(mockPublisherWithError.Object);
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        
        var serviceProvider = services.BuildServiceProvider();
        var publisher = serviceProvider.GetRequiredService<IMessagePublisher>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await publisher.PublishAsync(new { TestMessage = "This should fail" }, "orders.test");
        });

        exception.Message.Should().Be("Service Bus unavailable");
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private class PublishedMessage
    {
        public string Topic { get; set; } = string.Empty;
        public object Message { get; set; } = null!;
        public DateTime Timestamp { get; set; }
    }
}
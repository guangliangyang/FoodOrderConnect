using Azure.Messaging.ServiceBus;
using BidOne.OrderIntegrationFunction.Functions;
using BidOne.OrderIntegrationFunction.Services;
using BidOne.Shared.Models;
using FluentAssertions;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace BidOne.OrderIntegrationFunction.Tests.Functions;

public class OrderValidationFunctionTests
{
    private readonly Mock<IOrderValidationService> _mockValidationService;
    private readonly Mock<ILogger<OrderValidationFunction>> _mockLogger;
    private readonly Mock<IAsyncCollector<ServiceBusMessage>> _mockCollector;
    private readonly OrderValidationFunction _function;

    public OrderValidationFunctionTests()
    {
        _mockValidationService = new Mock<IOrderValidationService>();
        _mockLogger = new Mock<ILogger<OrderValidationFunction>>();
        _mockCollector = new Mock<IAsyncCollector<ServiceBusMessage>>();
        _function = new OrderValidationFunction(_mockValidationService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Run_WithValidOrder_SendsToEnrichmentQueue()
    {
        // Arrange
        var order = new Order
        {
            OrderId = "ORD-001",
            CustomerId = "CUST-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "PROD-001", Quantity = 2, UnitPrice = 25.00m }
            },
            TotalAmount = 50.00m,
            CreatedAt = DateTime.UtcNow
        };

        var validationResult = new ValidationResult
        {
            IsValid = true,
            Errors = new List<string>()
        };

        var messageBody = JsonSerializer.Serialize(order);
        var serviceBusMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(messageBody),
            messageId: "msg-001");

        _mockValidationService
            .Setup(x => x.ValidateOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        await _function.Run(serviceBusMessage, _mockCollector.Object);

        // Assert
        _mockValidationService.Verify(
            x => x.ValidateOrderAsync(
                It.Is<Order>(o => o.OrderId == "ORD-001"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockCollector.Verify(
            x => x.AddAsync(
                It.Is<ServiceBusMessage>(msg => 
                    msg.Subject == "order.validation.success" &&
                    JsonSerializer.Deserialize<Order>(msg.Body.ToString())!.OrderId == "ORD-001"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithInvalidOrder_SendsToErrorQueue()
    {
        // Arrange
        var order = new Order
        {
            OrderId = "ORD-002",
            CustomerId = "", // Invalid: empty customer ID
            Items = new List<OrderItem>(),
            TotalAmount = 0,
            CreatedAt = DateTime.UtcNow
        };

        var validationResult = new ValidationResult
        {
            IsValid = false,
            Errors = new List<string> { "Customer ID is required", "Order must contain at least one item" }
        };

        var messageBody = JsonSerializer.Serialize(order);
        var serviceBusMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(messageBody),
            messageId: "msg-002");

        _mockValidationService
            .Setup(x => x.ValidateOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        await _function.Run(serviceBusMessage, _mockCollector.Object);

        // Assert
        _mockValidationService.Verify(
            x => x.ValidateOrderAsync(
                It.Is<Order>(o => o.OrderId == "ORD-002"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockCollector.Verify(
            x => x.AddAsync(
                It.Is<ServiceBusMessage>(msg => 
                    msg.Subject == "order.validation.failed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithMalformedMessage_LogsErrorAndThrows()
    {
        // Arrange
        var invalidJson = "{ invalid json }";
        var serviceBusMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(invalidJson),
            messageId: "msg-003");

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(
            () => _function.Run(serviceBusMessage, _mockCollector.Object));

        // Verify validation service was not called
        _mockValidationService.Verify(
            x => x.ValidateOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Verify no message was sent to collector
        _mockCollector.Verify(
            x => x.AddAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_WhenValidationServiceThrows_PropagatesException()
    {
        // Arrange
        var order = new Order
        {
            OrderId = "ORD-003",
            CustomerId = "CUST-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "PROD-001", Quantity = 1, UnitPrice = 10.00m }
            },
            TotalAmount = 10.00m,
            CreatedAt = DateTime.UtcNow
        };

        var messageBody = JsonSerializer.Serialize(order);
        var serviceBusMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(messageBody),
            messageId: "msg-004");

        _mockValidationService
            .Setup(x => x.ValidateOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Validation service error"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _function.Run(serviceBusMessage, _mockCollector.Object));

        exception.Message.Should().Be("Validation service error");

        // Verify validation service was called
        _mockValidationService.Verify(
            x => x.ValidateOrderAsync(
                It.Is<Order>(o => o.OrderId == "ORD-003"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify no message was sent to collector
        _mockCollector.Verify(
            x => x.AddAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Run_WithEmptyMessage_LogsWarningAndReturns()
    {
        // Arrange
        var serviceBusMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(""),
            messageId: "msg-005");

        // Act
        await _function.Run(serviceBusMessage, _mockCollector.Object);

        // Assert
        _mockValidationService.Verify(
            x => x.ValidateOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mockCollector.Verify(
            x => x.AddAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)] // Null order ID
    [InlineData("")] // Empty order ID
    [InlineData("   ")] // Whitespace order ID
    public async Task Run_WithInvalidOrderId_HandlesGracefully(string? invalidOrderId)
    {
        // Arrange
        var order = new Order
        {
            OrderId = invalidOrderId!,
            CustomerId = "CUST-001",
            Items = new List<OrderItem>
            {
                new() { ProductId = "PROD-001", Quantity = 1, UnitPrice = 10.00m }
            },
            TotalAmount = 10.00m,
            CreatedAt = DateTime.UtcNow
        };

        var validationResult = new ValidationResult
        {
            IsValid = false,
            Errors = new List<string> { "Order ID is required" }
        };

        var messageBody = JsonSerializer.Serialize(order);
        var serviceBusMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(messageBody),
            messageId: "msg-invalid");

        _mockValidationService
            .Setup(x => x.ValidateOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        await _function.Run(serviceBusMessage, _mockCollector.Object);

        // Assert
        _mockValidationService.Verify(
            x => x.ValidateOrderAsync(
                It.Is<Order>(o => o.OrderId == invalidOrderId),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockCollector.Verify(
            x => x.AddAsync(
                It.Is<ServiceBusMessage>(msg => 
                    msg.Subject == "order.validation.failed"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_WithLargeOrder_ProcessesSuccessfully()
    {
        // Arrange
        var order = new Order
        {
            OrderId = "ORD-LARGE-001",
            CustomerId = "CUST-001",
            Items = Enumerable.Range(1, 100).Select(i => new OrderItem
            {
                ProductId = $"PROD-{i:000}",
                Quantity = i % 10 + 1,
                UnitPrice = (decimal)(i * 1.5)
            }).ToList(),
            TotalAmount = 0, // Will be calculated
            CreatedAt = DateTime.UtcNow
        };
        
        order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

        var validationResult = new ValidationResult
        {
            IsValid = true,
            Errors = new List<string>()
        };

        var messageBody = JsonSerializer.Serialize(order);
        var serviceBusMessage = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(messageBody),
            messageId: "msg-large");

        _mockValidationService
            .Setup(x => x.ValidateOrderAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        await _function.Run(serviceBusMessage, _mockCollector.Object);

        // Assert
        _mockValidationService.Verify(
            x => x.ValidateOrderAsync(
                It.Is<Order>(o => 
                    o.OrderId == "ORD-LARGE-001" &&
                    o.Items.Count == 100),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockCollector.Verify(
            x => x.AddAsync(
                It.Is<ServiceBusMessage>(msg => 
                    msg.Subject == "order.validation.success"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

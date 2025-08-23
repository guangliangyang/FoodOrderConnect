using BidOne.ExternalOrderApi.Services;
using BidOne.Shared.Models;
using BidOne.Shared.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;

namespace BidOne.ExternalOrderApi.Tests.Services;

public class OrderServiceTests
{
    private readonly Mock<IMessagePublisher> _mockMessagePublisher;
    private readonly Mock<ILogger<OrderService>> _mockLogger;
    private readonly OrderService _orderService;

    public OrderServiceTests()
    {
        _mockMessagePublisher = new Mock<IMessagePublisher>();
        _mockLogger = new Mock<ILogger<OrderService>>();
        _orderService = new OrderService(_mockMessagePublisher.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateOrderAsync_WithValidRequest_ReturnsOrderResponse()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = 10,
                    UnitPrice = 25.50m
                },
                new()
                {
                    ProductId = "PROD-002",
                    Quantity = 5,
                    UnitPrice = 12.75m
                }
            }
        };

        _mockMessagePublisher
            .Setup(x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _orderService.CreateOrderAsync(request, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.OrderId.Should().NotBeNullOrEmpty();
        result.OrderId.Should().StartWith("ORD-");
        result.Status.Should().Be(OrderStatus.Received);
        result.Message.Should().Be("Order received and queued for processing");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        // Verify message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(
                "orders.received",
                It.Is<Order>(o => 
                    o.CustomerId == request.CustomerId &&
                    o.Items.Count == 2 &&
                    o.TotalAmount == 319.25m), // (10 * 25.50) + (5 * 12.75)
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_WithEmptyCustomerId_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = 1,
                    UnitPrice = 10.00m
                }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _orderService.CreateOrderAsync(request, CancellationToken.None));
        
        exception.Message.Should().Contain("Customer ID is required");
        
        // Verify no message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_WithNullItems_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = null!
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _orderService.CreateOrderAsync(request, CancellationToken.None));
        
        exception.Message.Should().Contain("Order must contain at least one item");
        
        // Verify no message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_WithEmptyItems_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>()
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _orderService.CreateOrderAsync(request, CancellationToken.None));
        
        exception.Message.Should().Contain("Order must contain at least one item");
        
        // Verify no message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_WithInvalidItem_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "", // Invalid: empty product ID
                    Quantity = 1,
                    UnitPrice = 10.00m
                }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _orderService.CreateOrderAsync(request, CancellationToken.None));
        
        exception.Message.Should().Contain("Product ID is required");
        
        // Verify no message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_WithZeroQuantity_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = 0, // Invalid: zero quantity
                    UnitPrice = 10.00m
                }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _orderService.CreateOrderAsync(request, CancellationToken.None));
        
        exception.Message.Should().Contain("Quantity must be greater than 0");
        
        // Verify no message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_WithNegativePrice_ThrowsValidationException()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = 1,
                    UnitPrice = -10.00m // Invalid: negative price
                }
            }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _orderService.CreateOrderAsync(request, CancellationToken.None));
        
        exception.Message.Should().Contain("Unit price must be greater than 0");
        
        // Verify no message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenPublishingFails_ThrowsException()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = 1,
                    UnitPrice = 10.00m
                }
            }
        };

        _mockMessagePublisher
            .Setup(x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Message publishing failed"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _orderService.CreateOrderAsync(request, CancellationToken.None));
        
        exception.Message.Should().Be("Message publishing failed");
    }

    [Fact]
    public async Task CreateOrderAsync_WithCancellationToken_PropagatesCancellation()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = 1,
                    UnitPrice = 10.00m
                }
            }
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _orderService.CreateOrderAsync(request, cts.Token));
    }

    [Theory]
    [InlineData(1, 10.00, 10.00)]
    [InlineData(2, 15.50, 31.00)]
    [InlineData(10, 5.25, 52.50)]
    public async Task CreateOrderAsync_CalculatesTotalAmount_Correctly(int quantity, decimal unitPrice, decimal expectedTotal)
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = quantity,
                    UnitPrice = unitPrice
                }
            }
        };

        _mockMessagePublisher
            .Setup(x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _orderService.CreateOrderAsync(request, CancellationToken.None);

        // Assert
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(
                "orders.received",
                It.Is<Order>(o => o.TotalAmount == expectedTotal),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_GeneratesUniqueOrderIds()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "CUST-001",
            Items = new List<CreateOrderItemRequest>
            {
                new()
                {
                    ProductId = "PROD-001",
                    Quantity = 1,
                    UnitPrice = 10.00m
                }
            }
        };

        _mockMessagePublisher
            .Setup(x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act - create multiple orders
        var result1 = await _orderService.CreateOrderAsync(request, CancellationToken.None);
        var result2 = await _orderService.CreateOrderAsync(request, CancellationToken.None);
        var result3 = await _orderService.CreateOrderAsync(request, CancellationToken.None);

        // Assert
        result1.OrderId.Should().NotBe(result2.OrderId);
        result1.OrderId.Should().NotBe(result3.OrderId);
        result2.OrderId.Should().NotBe(result3.OrderId);
        
        // All should start with ORD- and contain date
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        result1.OrderId.Should().StartWith("ORD-").And.Contain(today);
        result2.OrderId.Should().StartWith("ORD-").And.Contain(today);
        result3.OrderId.Should().StartWith("ORD-").And.Contain(today);
    }
}

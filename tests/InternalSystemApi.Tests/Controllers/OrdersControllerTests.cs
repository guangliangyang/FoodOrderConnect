using BidOne.InternalSystemApi.Controllers;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.InternalSystemApi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace BidOne.InternalSystemApi.Tests.Controllers;

public class OrdersControllerTests
{
    private readonly Mock<IOrderProcessingService> _mockOrderProcessingService;
    private readonly Mock<ILogger<OrdersController>> _mockLogger;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        _mockOrderProcessingService = new Mock<IOrderProcessingService>();
        _mockLogger = new Mock<ILogger<OrdersController>>();
        _controller = new OrdersController(_mockOrderProcessingService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetOrders_ReturnsAllOrders()
    {
        // Arrange
        var expectedOrders = new List<OrderEntity>
        {
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-001", 
                CustomerId = "CUST-001", 
                Status = "Processing",
                TotalAmount = 100.00m,
                CreatedAt = DateTime.UtcNow
            },
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-002", 
                CustomerId = "CUST-002", 
                Status = "Completed",
                TotalAmount = 75.50m,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            }
        };

        _mockOrderProcessingService
            .Setup(x => x.GetOrdersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrders);

        // Act
        var result = await _controller.GetOrders();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var orders = okResult.Value.Should().BeAssignableTo<IEnumerable<OrderEntity>>().Subject;
        
        orders.Should().HaveCount(2);
        orders.Should().Contain(o => o.OrderId == "ORD-001");
        orders.Should().Contain(o => o.OrderId == "ORD-002");
        
        _mockOrderProcessingService.Verify(x => x.GetOrdersAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrder_WithExistingOrder_ReturnsOrder()
    {
        // Arrange
        var orderId = "ORD-001";
        var expectedOrder = new OrderEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CustomerId = "CUST-001",
            Status = "Processing",
            TotalAmount = 100.00m,
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItemEntity>
            {
                new() { 
                    Id = Guid.NewGuid(), 
                    ProductId = "PROD-001", 
                    Quantity = 2, 
                    UnitPrice = 50.00m 
                }
            }
        };

        _mockOrderProcessingService
            .Setup(x => x.GetOrderByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        // Act
        var result = await _controller.GetOrder(orderId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var order = okResult.Value.Should().BeOfType<OrderEntity>().Subject;
        
        order.OrderId.Should().Be(orderId);
        order.Items.Should().HaveCount(1);
        
        _mockOrderProcessingService.Verify(x => x.GetOrderByIdAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrder_WithNonExistentOrder_ReturnsNotFound()
    {
        // Arrange
        var orderId = "NON-EXISTENT";

        _mockOrderProcessingService
            .Setup(x => x.GetOrderByIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderEntity?)null);

        // Act
        var result = await _controller.GetOrder(orderId);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundResult>();
        
        _mockOrderProcessingService.Verify(x => x.GetOrderByIdAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithValidRequest_ReturnsUpdatedOrder()
    {
        // Arrange
        var orderId = "ORD-001";
        var newStatus = "Completed";
        var updateRequest = new { Status = newStatus };
        
        var updatedOrder = new OrderEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CustomerId = "CUST-001",
            Status = newStatus,
            TotalAmount = 100.00m,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow
        };

        _mockOrderProcessingService
            .Setup(x => x.UpdateOrderStatusAsync(orderId, newStatus, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedOrder);

        // Act
        var result = await _controller.UpdateOrderStatus(orderId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var order = okResult.Value.Should().BeOfType<OrderEntity>().Subject;
        
        order.OrderId.Should().Be(orderId);
        order.Status.Should().Be(newStatus);
        order.UpdatedAt.Should().NotBeNull();
        
        _mockOrderProcessingService.Verify(x => x.UpdateOrderStatusAsync(orderId, newStatus, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatus_WithNonExistentOrder_ReturnsNotFound()
    {
        // Arrange
        var orderId = "NON-EXISTENT";
        var updateRequest = new { Status = "Completed" };

        _mockOrderProcessingService
            .Setup(x => x.UpdateOrderStatusAsync(orderId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderEntity?)null);

        // Act
        var result = await _controller.UpdateOrderStatus(orderId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundResult>();
        
        _mockOrderProcessingService.Verify(
            x => x.UpdateOrderStatusAsync(orderId, "Completed", It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task GetOrdersByStatus_ReturnsFilteredOrders()
    {
        // Arrange
        var status = "Processing";
        var expectedOrders = new List<OrderEntity>
        {
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-001", 
                CustomerId = "CUST-001", 
                Status = status,
                TotalAmount = 100.00m,
                CreatedAt = DateTime.UtcNow
            },
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-003", 
                CustomerId = "CUST-003", 
                Status = status,
                TotalAmount = 200.00m,
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            }
        };

        _mockOrderProcessingService
            .Setup(x => x.GetOrdersByStatusAsync(status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrders);

        // Act
        var result = await _controller.GetOrdersByStatus(status);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var orders = okResult.Value.Should().BeAssignableTo<IEnumerable<OrderEntity>>().Subject;
        
        orders.Should().HaveCount(2);
        orders.Should().OnlyContain(o => o.Status == status);
        
        _mockOrderProcessingService.Verify(x => x.GetOrdersByStatusAsync(status, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrdersByCustomer_ReturnsCustomerOrders()
    {
        // Arrange
        var customerId = "CUST-001";
        var expectedOrders = new List<OrderEntity>
        {
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-001", 
                CustomerId = customerId, 
                Status = "Processing",
                TotalAmount = 100.00m,
                CreatedAt = DateTime.UtcNow
            },
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-004", 
                CustomerId = customerId, 
                Status = "Completed",
                TotalAmount = 150.00m,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _mockOrderProcessingService
            .Setup(x => x.GetOrdersByCustomerAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrders);

        // Act
        var result = await _controller.GetOrdersByCustomer(customerId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var orders = okResult.Value.Should().BeAssignableTo<IEnumerable<OrderEntity>>().Subject;
        
        orders.Should().HaveCount(2);
        orders.Should().OnlyContain(o => o.CustomerId == customerId);
        
        _mockOrderProcessingService.Verify(x => x.GetOrdersByCustomerAsync(customerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_WithValidOrder_ReturnsProcessedOrder()
    {
        // Arrange
        var orderId = "ORD-001";
        var processedOrder = new OrderEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CustomerId = "CUST-001",
            Status = "Processing",
            TotalAmount = 100.00m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow
        };

        _mockOrderProcessingService
            .Setup(x => x.ProcessOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedOrder);

        // Act
        var result = await _controller.ProcessOrder(orderId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var order = okResult.Value.Should().BeOfType<OrderEntity>().Subject;
        
        order.OrderId.Should().Be(orderId);
        order.Status.Should().Be("Processing");
        order.UpdatedAt.Should().NotBeNull();
        
        _mockOrderProcessingService.Verify(x => x.ProcessOrderAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_WithNonExistentOrder_ReturnsNotFound()
    {
        // Arrange
        var orderId = "NON-EXISTENT";

        _mockOrderProcessingService
            .Setup(x => x.ProcessOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderEntity?)null);

        // Act
        var result = await _controller.ProcessOrder(orderId);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundResult>();
        
        _mockOrderProcessingService.Verify(x => x.ProcessOrderAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrder_WhenProcessingFails_ReturnsInternalServerError()
    {
        // Arrange
        var orderId = "ORD-001";

        _mockOrderProcessingService
            .Setup(x => x.ProcessOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Processing failed"));

        // Act
        var result = await _controller.ProcessOrder(orderId);

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);
    }
}

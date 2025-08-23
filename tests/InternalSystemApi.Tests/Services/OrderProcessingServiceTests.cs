using BidOne.InternalSystemApi.Data;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.InternalSystemApi.Services;
using BidOne.Shared.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BidOne.InternalSystemApi.Tests.Services;

public class OrderProcessingServiceTests
{
    private readonly Mock<IMessagePublisher> _mockMessagePublisher;
    private readonly Mock<ISupplierNotificationService> _mockSupplierService;
    private readonly Mock<ILogger<OrderProcessingService>> _mockLogger;
    private readonly BidOneDbContext _context;
    private readonly OrderProcessingService _service;

    public OrderProcessingServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BidOneDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new BidOneDbContext(options);
        _mockMessagePublisher = new Mock<IMessagePublisher>();
        _mockSupplierService = new Mock<ISupplierNotificationService>();
        _mockLogger = new Mock<ILogger<OrderProcessingService>>();
        
        _service = new OrderProcessingService(
            _context,
            _mockMessagePublisher.Object,
            _mockSupplierService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GetOrdersAsync_ReturnsAllOrders()
    {
        // Arrange
        var orders = new List<OrderEntity>
        {
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-001", 
                CustomerId = "CUST-001", 
                Status = "Pending",
                TotalAmount = 100.00m,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            },
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-002", 
                CustomerId = "CUST-002", 
                Status = "Processing",
                TotalAmount = 75.50m,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            }
        };

        await _context.Orders.AddRangeAsync(orders);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOrdersAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(o => o.OrderId == "ORD-001");
        result.Should().Contain(o => o.OrderId == "ORD-002");
    }

    [Fact]
    public async Task GetOrderByIdAsync_WithExistingOrder_ReturnsOrder()
    {
        // Arrange
        var orderId = "ORD-001";
        var order = new OrderEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CustomerId = "CUST-001",
            Status = "Pending",
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

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOrderByIdAsync(orderId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.Items.Should().HaveCount(1);
        result.Items.First().ProductId.Should().Be("PROD-001");
    }

    [Fact]
    public async Task GetOrderByIdAsync_WithNonExistentOrder_ReturnsNull()
    {
        // Act
        var result = await _service.GetOrderByIdAsync("NON-EXISTENT", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrdersByStatusAsync_ReturnsFilteredOrders()
    {
        // Arrange
        var status = "Processing";
        var orders = new List<OrderEntity>
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
                OrderId = "ORD-002", 
                CustomerId = "CUST-002", 
                Status = "Completed", // Different status
                TotalAmount = 75.50m,
                CreatedAt = DateTime.UtcNow
            },
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-003", 
                CustomerId = "CUST-003", 
                Status = status,
                TotalAmount = 200.00m,
                CreatedAt = DateTime.UtcNow
            }
        };

        await _context.Orders.AddRangeAsync(orders);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOrdersByStatusAsync(status, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(o => o.Status == status);
        result.Should().Contain(o => o.OrderId == "ORD-001");
        result.Should().Contain(o => o.OrderId == "ORD-003");
    }

    [Fact]
    public async Task GetOrdersByCustomerAsync_ReturnsCustomerOrders()
    {
        // Arrange
        var customerId = "CUST-001";
        var orders = new List<OrderEntity>
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
                OrderId = "ORD-002", 
                CustomerId = "CUST-002", // Different customer
                Status = "Completed",
                TotalAmount = 75.50m,
                CreatedAt = DateTime.UtcNow
            },
            new() { 
                Id = Guid.NewGuid(), 
                OrderId = "ORD-003", 
                CustomerId = customerId, 
                Status = "Completed",
                TotalAmount = 150.00m,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        await _context.Orders.AddRangeAsync(orders);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOrdersByCustomerAsync(customerId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().OnlyContain(o => o.CustomerId == customerId);
        result.Should().Contain(o => o.OrderId == "ORD-001");
        result.Should().Contain(o => o.OrderId == "ORD-003");
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WithExistingOrder_UpdatesStatusAndReturnsOrder()
    {
        // Arrange
        var orderId = "ORD-001";
        var newStatus = "Processing";
        var order = new OrderEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CustomerId = "CUST-001",
            Status = "Pending",
            TotalAmount = 100.00m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        _mockMessagePublisher
            .Setup(x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UpdateOrderStatusAsync(orderId, newStatus, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.Status.Should().Be(newStatus);
        result.UpdatedAt.Should().NotBeNull();
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        // Verify database was updated
        var updatedOrder = await _context.Orders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(newStatus);
        
        // Verify message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(
                "orders.status-updated",
                It.Is<object>(o => 
                    o.GetType().GetProperty("OrderId")!.GetValue(o)!.ToString() == orderId &&
                    o.GetType().GetProperty("Status")!.GetValue(o)!.ToString() == newStatus),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_WithNonExistentOrder_ReturnsNull()
    {
        // Act
        var result = await _service.UpdateOrderStatusAsync("NON-EXISTENT", "Processing", CancellationToken.None);

        // Assert
        result.Should().BeNull();
        
        // Verify no message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderAsync_WithExistingOrder_ProcessesOrderAndNotifiesSupplier()
    {
        // Arrange
        var orderId = "ORD-001";
        var order = new OrderEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CustomerId = "CUST-001",
            Status = "Pending",
            TotalAmount = 100.00m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
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

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        _mockMessagePublisher
            .Setup(x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mockSupplierService
            .Setup(x => x.NotifySupplierAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ProcessOrderAsync(orderId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.Status.Should().Be("Processing");
        result.UpdatedAt.Should().NotBeNull();
        
        // Verify supplier notification was sent
        _mockSupplierService.Verify(
            x => x.NotifySupplierAsync(It.Is<OrderEntity>(o => o.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Once);
        
        // Verify status update message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(
                "orders.status-updated",
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessOrderAsync_WithNonExistentOrder_ReturnsNull()
    {
        // Act
        var result = await _service.ProcessOrderAsync("NON-EXISTENT", CancellationToken.None);

        // Assert
        result.Should().BeNull();
        
        // Verify no supplier notification was sent
        _mockSupplierService.Verify(
            x => x.NotifySupplierAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
        
        // Verify no message was published
        _mockMessagePublisher.Verify(
            x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessOrderAsync_WhenSupplierNotificationFails_StillUpdatesStatus()
    {
        // Arrange
        var orderId = "ORD-001";
        var order = new OrderEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CustomerId = "CUST-001",
            Status = "Pending",
            TotalAmount = 100.00m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        await _context.Orders.AddAsync(order);
        await _context.SaveChangesAsync();

        _mockMessagePublisher
            .Setup(x => x.PublishMessageAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        _mockSupplierService
            .Setup(x => x.NotifySupplierAsync(It.IsAny<OrderEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Supplier notification failed"));

        // Act
        var result = await _service.ProcessOrderAsync(orderId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be("Processing");
        
        // Verify database was still updated despite notification failure
        var updatedOrder = await _context.Orders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be("Processing");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

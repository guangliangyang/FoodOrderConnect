using BidOne.InternalSystemApi.Data;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BidOne.InternalSystemApi.Tests.Integration;

[Trait("Category", "Integration")]
public class DatabaseIntegrationTests : IDisposable
{
    protected readonly BidOneDbContext Context;
    protected readonly IServiceProvider ServiceProvider;
    private readonly ServiceCollection _services;

    public DatabaseIntegrationTests()
    {
        _services = new ServiceCollection();
        ConfigureServices(_services);
        ServiceProvider = _services.BuildServiceProvider();
        Context = ServiceProvider.GetRequiredService<BidOneDbContext>();
        
        // Ensure database is created
        Context.Database.EnsureCreated();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Configure in-memory database
        services.AddDbContext<BidOneDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        // Add logging
        services.AddLogging(builder => 
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    }

    protected async Task ClearDatabaseAsync()
    {
        Context.Orders.RemoveRange(Context.Orders);
        Context.OrderItems.RemoveRange(Context.OrderItems);
        Context.Customers.RemoveRange(Context.Customers);
        Context.Products.RemoveRange(Context.Products);
        Context.Inventory.RemoveRange(Context.Inventory);
        Context.Suppliers.RemoveRange(Context.Suppliers);
        Context.AuditLogs.RemoveRange(Context.AuditLogs);
        Context.OrderEvents.RemoveRange(Context.OrderEvents);
        
        await Context.SaveChangesAsync();
    }

    public virtual void Dispose()
    {
        Context.Dispose();
        ServiceProvider?.GetService<IServiceScope>()?.Dispose();
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
    [Fact]
    public async Task Database_CanCreateAndRetrieveOrder_Successfully()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var newOrder = new OrderEntity
        {
            Id = "ORD-INT-001",
            CustomerId = "CUST-INT-001",
            Status = OrderStatus.Processing,
            TotalAmount = 150.75m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = new List<OrderItemEntity>
            {
                new OrderItemEntity
                {
                    Id = Guid.NewGuid(),
                    ProductId = "PROD-INT-001",
                    Quantity = 2,
                    UnitPrice = 75.375m
                }
            }
        };

        // Act
        Context.Orders.Add(newOrder);
        await Context.SaveChangesAsync();

        var retrievedOrder = await Context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == "ORD-INT-001");

        // Assert
        retrievedOrder.Should().NotBeNull();
        retrievedOrder!.CustomerId.Should().Be("CUST-INT-001");
        retrievedOrder.Status.Should().Be(OrderStatus.Processing);
        retrievedOrder.TotalAmount.Should().Be(150.75m);
        retrievedOrder.Items.Should().HaveCount(1);
        retrievedOrder.Items.First().ProductId.Should().Be("PROD-INT-001");
        retrievedOrder.Items.First().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task Database_CanUpdateOrderStatus_Successfully()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var order = new OrderEntity
        {
            Id = "ORD-INT-002",
            CustomerId = "CUST-INT-002",
            Status = OrderStatus.Received,
            TotalAmount = 75.50m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        Context.Orders.Add(order);
        await Context.SaveChangesAsync();

        // Act
        var orderToUpdate = await Context.Orders
            .FirstOrDefaultAsync(o => o.Id == "ORD-INT-002");
        
        orderToUpdate!.Status = OrderStatus.Processing;
        orderToUpdate.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        var updatedOrder = await Context.Orders
            .FirstOrDefaultAsync(o => o.Id == "ORD-INT-002");
        
        updatedOrder.Should().NotBeNull();
        updatedOrder!.Status.Should().Be(OrderStatus.Processing);
        updatedOrder.UpdatedAt.Should().BeAfter(DateTime.MinValue);
        updatedOrder.UpdatedAt.Should().BeAfter(updatedOrder.CreatedAt);
    }

    [Fact]
    public async Task Database_CanQueryOrdersByStatus_Successfully()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var orders = new List<OrderEntity>
        {
            new OrderEntity
            {
                Id = "ORD-STATUS-001",
                CustomerId = "CUST-001",
                Status = OrderStatus.Processing,
                TotalAmount = 100.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new OrderEntity
            {
                Id = "ORD-STATUS-002",
                CustomerId = "CUST-002",
                Status = OrderStatus.Processing,
                TotalAmount = 200.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new OrderEntity
            {
                Id = "ORD-STATUS-003",
                CustomerId = "CUST-003",
                Status = OrderStatus.Delivered,
                TotalAmount = 300.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        Context.Orders.AddRange(orders);
        await Context.SaveChangesAsync();

        // Act
        var processingOrders = await Context.Orders
            .Where(o => o.Status == OrderStatus.Processing)
            .ToListAsync();

        // Assert
        processingOrders.Should().HaveCount(2);
        processingOrders.Should().OnlyContain(o => o.Status == OrderStatus.Processing);
        processingOrders.Should().Contain(o => o.Id == "ORD-STATUS-001");
        processingOrders.Should().Contain(o => o.Id == "ORD-STATUS-002");
    }

    [Fact]
    public async Task Database_CanManageInventory_Successfully()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var inventoryItem = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = "PROD-INV-001",
            QuantityOnHand = 100,
            QuantityReserved = 10,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Context.Inventory.Add(inventoryItem);
        await Context.SaveChangesAsync();

        // Act - Reserve more inventory
        var item = await Context.Inventory
            .FirstOrDefaultAsync(i => i.ProductId == "PROD-INV-001");
        
        item!.QuantityOnHand -= 5;
        item.QuantityReserved += 5;
        item.LastUpdated = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync();

        // Assert
        var updatedItem = await Context.Inventory
            .FirstOrDefaultAsync(i => i.ProductId == "PROD-INV-001");
        
        updatedItem.Should().NotBeNull();
        updatedItem!.AvailableQuantity.Should().Be(80); // 95 - 15 = 80
        updatedItem.QuantityReserved.Should().Be(15);
    }

    [Fact]
    public async Task Database_CanCreateAuditLog_Successfully()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var auditLog = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            EntityType = "Order",
            EntityId = "ORD-AUDIT-001",
            Action = "Created",
            Changes = "Order created with 2 items",
            UserId = "USER-001",
            Timestamp = DateTime.UtcNow
        };

        // Act
        Context.AuditLogs.Add(auditLog);
        await Context.SaveChangesAsync();

        var retrievedLog = await Context.AuditLogs
            .FirstOrDefaultAsync(a => a.EntityId == "ORD-AUDIT-001");

        // Assert
        retrievedLog.Should().NotBeNull();
        retrievedLog!.EntityType.Should().Be("Order");
        retrievedLog.Action.Should().Be("Created");
        retrievedLog.Changes.Should().Be("Order created with 2 items");
        retrievedLog.UserId.Should().Be("USER-001");
    }

    [Fact]
    public async Task Database_CanHandleConcurrentUpdates_Successfully()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var order = new OrderEntity
        {
            Id = "ORD-CONCURRENT-001",
            CustomerId = "CUST-CONCURRENT-001",
            Status = OrderStatus.Received,
            TotalAmount = 100.00m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        Context.Orders.Add(order);
        await Context.SaveChangesAsync();

        // Act - Simulate concurrent updates using separate contexts
        var context1 = ServiceProvider.GetRequiredService<BidOneDbContext>();
        var context2 = ServiceProvider.GetRequiredService<BidOneDbContext>();

        var order1 = await context1.Orders
            .FirstOrDefaultAsync(o => o.Id == "ORD-CONCURRENT-001");
        var order2 = await context2.Orders
            .FirstOrDefaultAsync(o => o.Id == "ORD-CONCURRENT-001");

        order1!.Status = OrderStatus.Processing;
        order2!.TotalAmount = 150.00m;

        await context1.SaveChangesAsync();
        await context2.SaveChangesAsync();

        // Assert
        var finalOrder = await Context.Orders
            .FirstOrDefaultAsync(o => o.Id == "ORD-CONCURRENT-001");
        
        finalOrder.Should().NotBeNull();
        // In-memory database will allow both updates
        finalOrder!.TotalAmount.Should().Be(150.00m);
    }

    [Fact]
    public async Task Database_CanQueryComplexRelationships_Successfully()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var customer = new CustomerEntity 
        { 
            Id = "CUST-REL-001", 
            Name = "Test Customer", 
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var product = new ProductEntity 
        { 
            Id = "PROD-REL-001", 
            Name = "Test Product", 
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var order = new OrderEntity
        {
            Id = "ORD-REL-001",
            CustomerId = customer.Id,
            Status = OrderStatus.Processing,
            TotalAmount = 200.00m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Items = new List<OrderItemEntity>
            {
                new OrderItemEntity
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Quantity = 2,
                    UnitPrice = 100.00m
                }
            }
        };

        Context.Customers.Add(customer);
        Context.Products.Add(product);
        Context.Orders.Add(order);
        await Context.SaveChangesAsync();

        // Act
        var orderWithRelations = await Context.Orders
            .Include(o => o.Items)
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == "ORD-REL-001");

        // Assert
        orderWithRelations.Should().NotBeNull();
        orderWithRelations!.Items.Should().HaveCount(1);
        orderWithRelations.Customer.Should().NotBeNull();
        orderWithRelations.Customer!.Id.Should().Be(customer.Id);
        orderWithRelations.Items.First().ProductId.Should().Be(product.Id);
    }
}
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.Shared.Domain.ValueObjects;
using BidOne.Shared.Models;

namespace BidOne.Shared.Tests.TestHelpers;

public static class TestDataFactory
{
    public static Order CreateValidOrder(string? orderId = null, string? customerId = null)
    {
        return new Order
        {
            OrderId = orderId ?? $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
            CustomerId = customerId ?? "CUST-001",
            Items = new List<OrderItem>
            {
                CreateValidOrderItem("PROD-001", 2, 25.00m),
                CreateValidOrderItem("PROD-002", 1, 50.00m)
            },
            TotalAmount = 100.00m,
            Status = OrderStatus.Received,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static OrderItem CreateValidOrderItem(string productId, int quantity, decimal unitPrice)
    {
        return new OrderItem
        {
            ProductId = productId,
            ProductName = $"Test Product {productId}",
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = quantity * unitPrice
        };
    }

    public static CreateOrderRequest CreateValidOrderRequest(string? customerId = null)
    {
        return new CreateOrderRequest
        {
            CustomerId = customerId ?? "CUST-001",
            Items = new List<CreateOrderItemRequest>
            {
                CreateValidOrderItemRequest("PROD-001", 2, 25.00m),
                CreateValidOrderItemRequest("PROD-002", 1, 50.00m)
            }
        };
    }

    public static CreateOrderItemRequest CreateValidOrderItemRequest(string productId, int quantity, decimal unitPrice)
    {
        return new CreateOrderItemRequest
        {
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
    }

    public static OrderEntity CreateValidOrderEntity(string? orderId = null, string? customerId = null)
    {
        var entity = new OrderEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId ?? $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20],
            CustomerId = customerId ?? "CUST-001",
            Status = "Pending",
            TotalAmount = 100.00m,
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItemEntity>()
        };

        entity.Items.Add(CreateValidOrderItemEntity(entity.Id, "PROD-001", 2, 25.00m));
        entity.Items.Add(CreateValidOrderItemEntity(entity.Id, "PROD-002", 1, 50.00m));

        return entity;
    }

    public static OrderItemEntity CreateValidOrderItemEntity(Guid orderId, string productId, int quantity, decimal unitPrice)
    {
        return new OrderItemEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            ProductId = productId,
            ProductName = $"Test Product {productId}",
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = quantity * unitPrice
        };
    }

    public static InventoryEntity CreateValidInventoryEntity(string productId, int available = 100, int reserved = 0)
    {
        return new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = $"Test Product {productId}",
            QuantityAvailable = available,
            QuantityReserved = reserved,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };
    }

    public static CustomerEntity CreateValidCustomerEntity(string? customerId = null)
    {
        return new CustomerEntity
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId ?? "CUST-001",
            Name = "Test Customer",
            Email = "test@example.com",
            Phone = "+1234567890",
            Address = "123 Test Street, Test City, TC 12345",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static ProductEntity CreateValidProductEntity(string? productId = null)
    {
        return new ProductEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId ?? "PROD-001",
            Name = $"Test Product {productId ?? "001"}",
            Description = "A test product for unit testing",
            Price = 25.00m,
            Category = "Test Category",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static SupplierEntity CreateValidSupplierEntity(string? supplierId = null)
    {
        return new SupplierEntity
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId ?? "SUPP-001",
            Name = "Test Supplier",
            ContactEmail = "supplier@example.com",
            ContactPhone = "+1234567890",
            Address = "456 Supplier Avenue, Supplier City, SC 67890",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static ValidationResult CreateValidValidationResult()
    {
        return new ValidationResult
        {
            IsValid = true,
            Errors = new List<string>()
        };
    }

    public static ValidationResult CreateInvalidValidationResult(params string[] errors)
    {
        return new ValidationResult
        {
            IsValid = false,
            Errors = errors.ToList()
        };
    }

    public static Money CreateValidMoney(decimal amount = 100.00m, string currency = "USD")
    {
        return new Money(amount, currency);
    }

    public static CustomerId CreateValidCustomerId(string? id = null)
    {
        return new CustomerId(id ?? "CUST-001");
    }

    public static OrderId CreateValidOrderId(string? id = null)
    {
        return new OrderId(id ?? $"ORD-{DateTime.UtcNow:yyyyMMdd}-TEST");
    }

    public static Quantity CreateValidQuantity(int value = 1)
    {
        return new Quantity(value);
    }

    public static ProductInfo CreateValidProductInfo(string? productId = null, string? name = null)
    {
        return new ProductInfo(
            productId ?? "PROD-001", 
            name ?? "Test Product",
            "Test Description",
            "Test Category");
    }

    // Helper methods for creating collections
    public static List<Order> CreateOrderList(int count = 3)
    {
        var orders = new List<Order>();
        for (int i = 1; i <= count; i++)
        {
            orders.Add(CreateValidOrder($"ORD-{i:000}", $"CUST-{i:000}"));
        }
        return orders;
    }

    public static List<OrderEntity> CreateOrderEntityList(int count = 3)
    {
        var orders = new List<OrderEntity>();
        for (int i = 1; i <= count; i++)
        {
            orders.Add(CreateValidOrderEntity($"ORD-{i:000}", $"CUST-{i:000}"));
        }
        return orders;
    }

    public static List<InventoryEntity> CreateInventoryEntityList(int count = 3)
    {
        var items = new List<InventoryEntity>();
        for (int i = 1; i <= count; i++)
        {
            items.Add(CreateValidInventoryEntity($"PROD-{i:000}", 100 - (i * 10), i * 2));
        }
        return items;
    }

    public static Dictionary<string, int> CreateInventoryCheckRequest()
    {
        return new Dictionary<string, int>
        {
            { "PROD-001", 10 },
            { "PROD-002", 5 },
            { "PROD-003", 15 }
        };
    }

    // Helper method for creating test dates
    public static DateTime GetTestDate(int daysFromNow = 0, int hoursFromNow = 0)
    {
        return DateTime.UtcNow.AddDays(daysFromNow).AddHours(hoursFromNow);
    }

    // Helper method for creating random test data
    public static string GenerateRandomOrderId()
    {
        return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..20];
    }

    public static string GenerateRandomCustomerId()
    {
        return $"CUST-{Guid.NewGuid():N}"[..10].ToUpper();
    }

    public static string GenerateRandomProductId()
    {
        return $"PROD-{Guid.NewGuid():N}"[..10].ToUpper();
    }
}

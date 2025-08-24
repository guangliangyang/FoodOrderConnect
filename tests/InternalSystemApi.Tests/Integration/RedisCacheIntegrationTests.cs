using BidOne.InternalSystemApi.Data;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.Shared.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using Testcontainers.Redis;
using Xunit;

namespace BidOne.InternalSystemApi.Tests.Integration;

[Trait("Category", "Integration")]
public class RedisCacheIntegrationTests : IAsyncDisposable
{
    private readonly RedisContainer _redisContainer;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedCache _cache;
    private readonly BidOneDbContext _context;

    public RedisCacheIntegrationTests()
    {
        // Setup Redis container for integration testing
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithName($"redis-test-{Guid.NewGuid():N}")
            .Build();

        // Start container (this is async, but constructor can't be async)
        _redisContainer.StartAsync().Wait();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        _cache = _serviceProvider.GetRequiredService<IDistributedCache>();
        _context = _serviceProvider.GetRequiredService<BidOneDbContext>();

        // Ensure database is created
        _context.Database.EnsureCreated();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Configure Redis cache using the test container
        var connectionString = _redisContainer.GetConnectionString();
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = connectionString;
            options.InstanceName = "BidOneTestCache";
        });

        // Configure in-memory database
        services.AddDbContext<BidOneDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        // Add logging
        services.AddLogging(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
    }

    [Fact]
    public async Task Redis_CanStoreAndRetrieveString_Successfully()
    {
        // Arrange
        var key = "test-string-key";
        var value = "Hello Redis Integration Test!";

        // Act
        await _cache.SetStringAsync(key, value);
        var retrievedValue = await _cache.GetStringAsync(key);

        // Assert
        retrievedValue.Should().Be(value);
    }

    [Fact]
    public async Task Redis_CanStoreAndRetrieveJsonObject_Successfully()
    {
        // Arrange
        var key = "test-order-cache";
        var order = new OrderEntity
        {
            Id = "ORD-CACHE-001",
            CustomerId = "CUST-001",
            Status = OrderStatus.Processing,
            TotalAmount = 150.75m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var orderJson = JsonSerializer.Serialize(order);

        // Act
        await _cache.SetStringAsync(key, orderJson);
        var retrievedJson = await _cache.GetStringAsync(key);
        var retrievedOrder = JsonSerializer.Deserialize<OrderEntity>(retrievedJson!);

        // Assert
        retrievedOrder.Should().NotBeNull();
        retrievedOrder!.Id.Should().Be(order.Id);
        retrievedOrder.CustomerId.Should().Be(order.CustomerId);
        retrievedOrder.Status.Should().Be(order.Status);
        retrievedOrder.TotalAmount.Should().Be(order.TotalAmount);
    }

    [Fact]
    public async Task Redis_CanHandleExpiration_Successfully()
    {
        // Arrange
        var key = "test-expiring-key";
        var value = "This will expire soon";
        var expiration = TimeSpan.FromSeconds(2);

        // Act
        await _cache.SetStringAsync(key, value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        });

        // Verify it exists initially
        var initialValue = await _cache.GetStringAsync(key);
        initialValue.Should().Be(value);

        // Wait for expiration
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Verify it's expired
        var expiredValue = await _cache.GetStringAsync(key);

        // Assert
        expiredValue.Should().BeNull();
    }

    [Fact]
    public async Task Redis_CanRemoveKeys_Successfully()
    {
        // Arrange
        var key = "test-removable-key";
        var value = "This will be removed";

        // Act
        await _cache.SetStringAsync(key, value);
        var valueBeforeRemoval = await _cache.GetStringAsync(key);
        
        await _cache.RemoveAsync(key);
        var valueAfterRemoval = await _cache.GetStringAsync(key);

        // Assert
        valueBeforeRemoval.Should().Be(value);
        valueAfterRemoval.Should().BeNull();
    }

    [Fact]
    public async Task Redis_CanHandleConcurrentOperations_Successfully()
    {
        // Arrange
        var baseKey = "concurrent-test";
        var tasks = new List<Task>();

        // Act - Create multiple concurrent write operations
        for (int i = 0; i < 10; i++)
        {
            var taskIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                var key = $"{baseKey}-{taskIndex}";
                var value = $"Value for task {taskIndex}";
                await _cache.SetStringAsync(key, value);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Verify all values were written correctly
        for (int i = 0; i < 10; i++)
        {
            var key = $"{baseKey}-{i}";
            var expectedValue = $"Value for task {i}";
            var actualValue = await _cache.GetStringAsync(key);
            actualValue.Should().Be(expectedValue);
        }
    }

    [Fact]
    public async Task Redis_CanStoreComplexDataStructures_Successfully()
    {
        // Arrange
        var key = "test-complex-data";
        var complexData = new Dictionary<string, object>
        {
            ["orderId"] = "ORD-COMPLEX-001",
            ["customerInfo"] = new { Name = "John Doe", Email = "john@example.com" },
            ["items"] = new[]
            {
                new { ProductId = "PROD-001", Quantity = 2, Price = 25.50m },
                new { ProductId = "PROD-002", Quantity = 1, Price = 50.00m }
            },
            ["metadata"] = new Dictionary<string, string>
            {
                ["source"] = "web",
                ["channel"] = "desktop"
            }
        };

        var jsonData = JsonSerializer.Serialize(complexData);

        // Act
        await _cache.SetStringAsync(key, jsonData);
        var retrievedJson = await _cache.GetStringAsync(key);
        var retrievedData = JsonSerializer.Deserialize<Dictionary<string, object>>(retrievedJson!);

        // Assert
        retrievedData.Should().NotBeNull();
        retrievedData.Should().ContainKey("orderId");
        retrievedData.Should().ContainKey("customerInfo");
        retrievedData.Should().ContainKey("items");
        retrievedData.Should().ContainKey("metadata");
    }

    [Fact]
    public async Task Redis_CanCacheOrderStatusWithDatabase_Successfully()
    {
        // Arrange
        await ClearDatabaseAsync();
        
        var order = new OrderEntity
        {
            Id = "ORD-STATUS-CACHE-001",
            CustomerId = "CUST-001",
            Status = OrderStatus.Processing,
            TotalAmount = 200.00m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var cacheKey = $"order-status-{order.Id}";
        var statusInfo = new
        {
            OrderId = order.Id,
            Status = order.Status.ToString(),
            LastUpdated = order.UpdatedAt,
            TotalAmount = order.TotalAmount
        };

        // Act - Cache the order status
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(statusInfo));

        // Simulate retrieving from cache instead of database
        var cachedStatusJson = await _cache.GetStringAsync(cacheKey);
        var cachedStatus = JsonSerializer.Deserialize<JsonElement>(cachedStatusJson!);

        // Assert
        cachedStatus.Should().NotBeNull();
        cachedStatusJson.Should().Contain(order.Id);
        cachedStatusJson.Should().Contain("Processing");
        cachedStatusJson.Should().Contain("200");
    }

    private async Task ClearDatabaseAsync()
    {
        _context.Orders.RemoveRange(_context.Orders);
        _context.OrderItems.RemoveRange(_context.OrderItems);
        _context.Customers.RemoveRange(_context.Customers);
        _context.Products.RemoveRange(_context.Products);
        _context.Inventory.RemoveRange(_context.Inventory);
        _context.Suppliers.RemoveRange(_context.Suppliers);
        _context.AuditLogs.RemoveRange(_context.AuditLogs);
        _context.OrderEvents.RemoveRange(_context.OrderEvents);
        
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _context.Dispose();
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        
        // Stop and cleanup Redis container
        await _redisContainer.StopAsync();
        await _redisContainer.DisposeAsync();
    }
}
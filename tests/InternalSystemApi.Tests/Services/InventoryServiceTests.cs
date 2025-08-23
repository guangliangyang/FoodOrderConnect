using BidOne.InternalSystemApi.Data;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.InternalSystemApi.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BidOne.InternalSystemApi.Tests.Services;

public class InventoryServiceTests : IDisposable
{
    private readonly BidOneDbContext _context;
    private readonly Mock<ILogger<InventoryService>> _mockLogger;
    private readonly InventoryService _service;

    public InventoryServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BidOneDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new BidOneDbContext(options);
        _mockLogger = new Mock<ILogger<InventoryService>>();
        _service = new InventoryService(_context, _mockLogger.Object);
    }

    [Fact]
    public async Task GetAllInventoryAsync_ReturnsAllInventoryItems()
    {
        // Arrange
        var items = new List<InventoryEntity>
        {
            new() {
                Id = Guid.NewGuid(),
                ProductId = "PROD-001",
                ProductName = "Product 1",
                QuantityAvailable = 100,
                QuantityReserved = 10,
                ReorderLevel = 20,
                LastUpdated = DateTime.UtcNow
            },
            new() {
                Id = Guid.NewGuid(),
                ProductId = "PROD-002",
                ProductName = "Product 2",
                QuantityAvailable = 50,
                QuantityReserved = 5,
                ReorderLevel = 15,
                LastUpdated = DateTime.UtcNow
            }
        };

        await _context.Inventory.AddRangeAsync(items);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllInventoryAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(i => i.ProductId == "PROD-001");
        result.Should().Contain(i => i.ProductId == "PROD-002");
    }

    [Fact]
    public async Task GetInventoryByProductIdAsync_WithExistingProduct_ReturnsItem()
    {
        // Arrange
        var productId = "PROD-001";
        var item = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = 100,
            QuantityReserved = 10,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Inventory.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetInventoryByProductIdAsync(productId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ProductId.Should().Be(productId);
        result.ProductName.Should().Be("Product 1");
        result.QuantityAvailable.Should().Be(100);
    }

    [Fact]
    public async Task GetInventoryByProductIdAsync_WithNonExistentProduct_ReturnsNull()
    {
        // Act
        var result = await _service.GetInventoryByProductIdAsync("NON-EXISTENT", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateInventoryQuantityAsync_WithExistingProduct_UpdatesQuantityAndReturnsItem()
    {
        // Arrange
        var productId = "PROD-001";
        var newQuantity = 150;
        var item = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = 100,
            QuantityReserved = 10,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow.AddHours(-1)
        };

        await _context.Inventory.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.UpdateInventoryQuantityAsync(productId, newQuantity, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ProductId.Should().Be(productId);
        result.QuantityAvailable.Should().Be(newQuantity);
        result.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        
        // Verify database was updated
        var updatedItem = await _context.Inventory.FindAsync(item.Id);
        updatedItem!.QuantityAvailable.Should().Be(newQuantity);
    }

    [Fact]
    public async Task UpdateInventoryQuantityAsync_WithNonExistentProduct_ReturnsNull()
    {
        // Act
        var result = await _service.UpdateInventoryQuantityAsync("NON-EXISTENT", 100, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ReserveInventoryAsync_WithSufficientQuantity_ReturnsTrue()
    {
        // Arrange
        var productId = "PROD-001";
        var quantityToReserve = 20;
        var item = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = 100,
            QuantityReserved = 10,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Inventory.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReserveInventoryAsync(productId, quantityToReserve, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        // Verify inventory was updated
        var updatedItem = await _context.Inventory.FindAsync(item.Id);
        updatedItem!.QuantityAvailable.Should().Be(80); // 100 - 20
        updatedItem.QuantityReserved.Should().Be(30); // 10 + 20
    }

    [Fact]
    public async Task ReserveInventoryAsync_WithInsufficientQuantity_ReturnsFalse()
    {
        // Arrange
        var productId = "PROD-001";
        var quantityToReserve = 150; // More than available
        var item = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = 100,
            QuantityReserved = 10,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Inventory.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReserveInventoryAsync(productId, quantityToReserve, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        
        // Verify inventory was not changed
        var unchangedItem = await _context.Inventory.FindAsync(item.Id);
        unchangedItem!.QuantityAvailable.Should().Be(100);
        unchangedItem.QuantityReserved.Should().Be(10);
    }

    [Fact]
    public async Task ReserveInventoryAsync_WithNonExistentProduct_ReturnsFalse()
    {
        // Act
        var result = await _service.ReserveInventoryAsync("NON-EXISTENT", 10, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseReservedInventoryAsync_WithSufficientReserved_ReturnsTrue()
    {
        // Arrange
        var productId = "PROD-001";
        var quantityToRelease = 5;
        var item = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = 90,
            QuantityReserved = 20,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Inventory.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReleaseReservedInventoryAsync(productId, quantityToRelease, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        
        // Verify inventory was updated
        var updatedItem = await _context.Inventory.FindAsync(item.Id);
        updatedItem!.QuantityAvailable.Should().Be(95); // 90 + 5
        updatedItem.QuantityReserved.Should().Be(15); // 20 - 5
    }

    [Fact]
    public async Task ReleaseReservedInventoryAsync_WithInsufficientReserved_ReturnsFalse()
    {
        // Arrange
        var productId = "PROD-001";
        var quantityToRelease = 25; // More than reserved
        var item = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = 90,
            QuantityReserved = 20,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Inventory.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReleaseReservedInventoryAsync(productId, quantityToRelease, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        
        // Verify inventory was not changed
        var unchangedItem = await _context.Inventory.FindAsync(item.Id);
        unchangedItem!.QuantityAvailable.Should().Be(90);
        unchangedItem.QuantityReserved.Should().Be(20);
    }

    [Fact]
    public async Task GetLowStockItemsAsync_ReturnsItemsBelowReorderLevel()
    {
        // Arrange
        var items = new List<InventoryEntity>
        {
            new() {
                Id = Guid.NewGuid(),
                ProductId = "PROD-001",
                ProductName = "Normal Stock Product",
                QuantityAvailable = 50,
                QuantityReserved = 5,
                ReorderLevel = 20, // Above reorder level
                LastUpdated = DateTime.UtcNow
            },
            new() {
                Id = Guid.NewGuid(),
                ProductId = "PROD-002",
                ProductName = "Low Stock Product",
                QuantityAvailable = 10,
                QuantityReserved = 2,
                ReorderLevel = 20, // Below reorder level
                LastUpdated = DateTime.UtcNow
            },
            new() {
                Id = Guid.NewGuid(),
                ProductId = "PROD-003",
                ProductName = "Very Low Stock Product",
                QuantityAvailable = 5,
                QuantityReserved = 1,
                ReorderLevel = 15, // Below reorder level
                LastUpdated = DateTime.UtcNow
            }
        };

        await _context.Inventory.AddRangeAsync(items);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetLowStockItemsAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(i => i.ProductId == "PROD-002");
        result.Should().Contain(i => i.ProductId == "PROD-003");
        result.Should().NotContain(i => i.ProductId == "PROD-001");
        result.Should().OnlyContain(i => i.QuantityAvailable < i.ReorderLevel);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_WithMultipleProducts_ReturnsCorrectAvailability()
    {
        // Arrange
        var items = new List<InventoryEntity>
        {
            new() {
                Id = Guid.NewGuid(),
                ProductId = "PROD-001",
                ProductName = "Product 1",
                QuantityAvailable = 100,
                QuantityReserved = 10,
                ReorderLevel = 20,
                LastUpdated = DateTime.UtcNow
            },
            new() {
                Id = Guid.NewGuid(),
                ProductId = "PROD-002",
                ProductName = "Product 2",
                QuantityAvailable = 5,
                QuantityReserved = 2,
                ReorderLevel = 15,
                LastUpdated = DateTime.UtcNow
            }
        };

        await _context.Inventory.AddRangeAsync(items);
        await _context.SaveChangesAsync();

        var requestedItems = new Dictionary<string, int>
        {
            { "PROD-001", 50 }, // Available: 100, so this should be true
            { "PROD-002", 10 }, // Available: 5, so this should be false
            { "PROD-003", 5 }   // Doesn't exist, so this should be false
        };

        // Act
        var result = await _service.CheckAvailabilityAsync(requestedItems, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result["PROD-001"].Should().BeTrue();
        result["PROD-002"].Should().BeFalse();
        result["PROD-003"].Should().BeFalse();
    }

    [Theory]
    [InlineData(0)] // Zero quantity
    [InlineData(-5)] // Negative quantity
    public async Task ReserveInventoryAsync_WithInvalidQuantity_ReturnsFalse(int invalidQuantity)
    {
        // Arrange
        var productId = "PROD-001";
        var item = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = 100,
            QuantityReserved = 10,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Inventory.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReserveInventoryAsync(productId, invalidQuantity, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        
        // Verify inventory was not changed
        var unchangedItem = await _context.Inventory.FindAsync(item.Id);
        unchangedItem!.QuantityAvailable.Should().Be(100);
        unchangedItem.QuantityReserved.Should().Be(10);
    }

    [Theory]
    [InlineData(0)] // Zero quantity
    [InlineData(-3)] // Negative quantity
    public async Task ReleaseReservedInventoryAsync_WithInvalidQuantity_ReturnsFalse(int invalidQuantity)
    {
        // Arrange
        var productId = "PROD-001";
        var item = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = 90,
            QuantityReserved = 20,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };

        await _context.Inventory.AddAsync(item);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ReleaseReservedInventoryAsync(productId, invalidQuantity, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
        
        // Verify inventory was not changed
        var unchangedItem = await _context.Inventory.FindAsync(item.Id);
        unchangedItem!.QuantityAvailable.Should().Be(90);
        unchangedItem.QuantityReserved.Should().Be(20);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

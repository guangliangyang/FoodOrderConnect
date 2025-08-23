using BidOne.InternalSystemApi.Controllers;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.InternalSystemApi.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace BidOne.InternalSystemApi.Tests.Controllers;

public class InventoryControllerTests
{
    private readonly Mock<IInventoryService> _mockInventoryService;
    private readonly Mock<ILogger<InventoryController>> _mockLogger;
    private readonly InventoryController _controller;

    public InventoryControllerTests()
    {
        _mockInventoryService = new Mock<IInventoryService>();
        _mockLogger = new Mock<ILogger<InventoryController>>();
        _controller = new InventoryController(_mockInventoryService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetInventory_ReturnsAllInventoryItems()
    {
        // Arrange
        var expectedItems = new List<InventoryEntity>
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
                LastUpdated = DateTime.UtcNow.AddHours(-1)
            }
        };

        _mockInventoryService
            .Setup(x => x.GetAllInventoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItems);

        // Act
        var result = await _controller.GetInventory();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = okResult.Value.Should().BeAssignableTo<IEnumerable<InventoryEntity>>().Subject;
        
        items.Should().HaveCount(2);
        items.Should().Contain(i => i.ProductId == "PROD-001");
        items.Should().Contain(i => i.ProductId == "PROD-002");
        
        _mockInventoryService.Verify(x => x.GetAllInventoryAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInventoryByProduct_WithExistingProduct_ReturnsInventoryItem()
    {
        // Arrange
        var productId = "PROD-001";
        var expectedItem = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = 100,
            QuantityReserved = 10,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };

        _mockInventoryService
            .Setup(x => x.GetInventoryByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedItem);

        // Act
        var result = await _controller.GetInventoryByProduct(productId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var item = okResult.Value.Should().BeOfType<InventoryEntity>().Subject;
        
        item.ProductId.Should().Be(productId);
        item.QuantityAvailable.Should().Be(100);
        
        _mockInventoryService.Verify(x => x.GetInventoryByProductIdAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInventoryByProduct_WithNonExistentProduct_ReturnsNotFound()
    {
        // Arrange
        var productId = "NON-EXISTENT";

        _mockInventoryService
            .Setup(x => x.GetInventoryByProductIdAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryEntity?)null);

        // Act
        var result = await _controller.GetInventoryByProduct(productId);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundResult>();
        
        _mockInventoryService.Verify(x => x.GetInventoryByProductIdAsync(productId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateInventoryQuantity_WithValidRequest_ReturnsUpdatedItem()
    {
        // Arrange
        var productId = "PROD-001";
        var newQuantity = 150;
        var updateRequest = new { QuantityAvailable = newQuantity };
        
        var updatedItem = new InventoryEntity
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = "Product 1",
            QuantityAvailable = newQuantity,
            QuantityReserved = 10,
            ReorderLevel = 20,
            LastUpdated = DateTime.UtcNow
        };

        _mockInventoryService
            .Setup(x => x.UpdateInventoryQuantityAsync(productId, newQuantity, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedItem);

        // Act
        var result = await _controller.UpdateInventoryQuantity(productId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var item = okResult.Value.Should().BeOfType<InventoryEntity>().Subject;
        
        item.ProductId.Should().Be(productId);
        item.QuantityAvailable.Should().Be(newQuantity);
        
        _mockInventoryService.Verify(x => x.UpdateInventoryQuantityAsync(productId, newQuantity, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateInventoryQuantity_WithNonExistentProduct_ReturnsNotFound()
    {
        // Arrange
        var productId = "NON-EXISTENT";
        var updateRequest = new { QuantityAvailable = 100 };

        _mockInventoryService
            .Setup(x => x.UpdateInventoryQuantityAsync(productId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryEntity?)null);

        // Act
        var result = await _controller.UpdateInventoryQuantity(productId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundResult>();
        
        _mockInventoryService.Verify(
            x => x.UpdateInventoryQuantityAsync(productId, 100, It.IsAny<CancellationToken>()), 
            Times.Once);
    }

    [Fact]
    public async Task ReserveInventory_WithSufficientQuantity_ReturnsSuccess()
    {
        // Arrange
        var productId = "PROD-001";
        var quantityToReserve = 5;
        var reserveRequest = new { Quantity = quantityToReserve };
        
        _mockInventoryService
            .Setup(x => x.ReserveInventoryAsync(productId, quantityToReserve, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ReserveInventory(productId, reserveRequest);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var response = okResult.Value as dynamic;
        response.Should().NotBeNull();
        
        _mockInventoryService.Verify(x => x.ReserveInventoryAsync(productId, quantityToReserve, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveInventory_WithInsufficientQuantity_ReturnsBadRequest()
    {
        // Arrange
        var productId = "PROD-001";
        var quantityToReserve = 1000; // More than available
        var reserveRequest = new { Quantity = quantityToReserve };
        
        _mockInventoryService
            .Setup(x => x.ReserveInventoryAsync(productId, quantityToReserve, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.ReserveInventory(productId, reserveRequest);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<BadRequestObjectResult>();
        
        _mockInventoryService.Verify(x => x.ReserveInventoryAsync(productId, quantityToReserve, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseReservedInventory_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var productId = "PROD-001";
        var quantityToRelease = 3;
        var releaseRequest = new { Quantity = quantityToRelease };
        
        _mockInventoryService
            .Setup(x => x.ReleaseReservedInventoryAsync(productId, quantityToRelease, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ReleaseReservedInventory(productId, releaseRequest);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var response = okResult.Value as dynamic;
        response.Should().NotBeNull();
        
        _mockInventoryService.Verify(x => x.ReleaseReservedInventoryAsync(productId, quantityToRelease, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLowStockItems_ReturnsItemsBelowReorderLevel()
    {
        // Arrange
        var lowStockItems = new List<InventoryEntity>
        {
            new() { 
                Id = Guid.NewGuid(), 
                ProductId = "PROD-003", 
                ProductName = "Low Stock Product",
                QuantityAvailable = 5, // Below reorder level
                QuantityReserved = 2,
                ReorderLevel = 20,
                LastUpdated = DateTime.UtcNow
            }
        };

        _mockInventoryService
            .Setup(x => x.GetLowStockItemsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(lowStockItems);

        // Act
        var result = await _controller.GetLowStockItems();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = okResult.Value.Should().BeAssignableTo<IEnumerable<InventoryEntity>>().Subject;
        
        items.Should().HaveCount(1);
        items.First().ProductId.Should().Be("PROD-003");
        items.First().QuantityAvailable.Should().BeLessThan(items.First().ReorderLevel);
        
        _mockInventoryService.Verify(x => x.GetLowStockItemsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckAvailability_WithMultipleProducts_ReturnsAvailabilityStatus()
    {
        // Arrange
        var checkRequest = new
        {
            Items = new[]
            {
                new { ProductId = "PROD-001", Quantity = 10 },
                new { ProductId = "PROD-002", Quantity = 5 }
            }
        };
        
        var availabilityResults = new Dictionary<string, bool>
        {
            { "PROD-001", true },
            { "PROD-002", false }
        };

        _mockInventoryService
            .Setup(x => x.CheckAvailabilityAsync(It.IsAny<Dictionary<string, int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(availabilityResults);

        // Act
        var result = await _controller.CheckAvailability(checkRequest);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        var response = okResult.Value as Dictionary<string, bool>;
        response.Should().NotBeNull();
        response.Should().HaveCount(2);
        response["PROD-001"].Should().BeTrue();
        response["PROD-002"].Should().BeFalse();
        
        _mockInventoryService.Verify(
            x => x.CheckAvailabilityAsync(
                It.Is<Dictionary<string, int>>(d => 
                    d.ContainsKey("PROD-001") && d["PROD-001"] == 10 &&
                    d.ContainsKey("PROD-002") && d["PROD-002"] == 5), 
                It.IsAny<CancellationToken>()), 
            Times.Once);
    }
}

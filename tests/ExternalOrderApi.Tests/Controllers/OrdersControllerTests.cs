using BidOne.ExternalOrderApi.Controllers;
using BidOne.ExternalOrderApi.Services;
using BidOne.Shared.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel.DataAnnotations;

namespace BidOne.ExternalOrderApi.Tests.Controllers;

public class OrdersControllerTests
{
    private readonly Mock<IOrderService> _mockOrderService;
    private readonly Mock<ILogger<OrdersController>> _mockLogger;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        _mockOrderService = new Mock<IOrderService>();
        _mockLogger = new Mock<ILogger<OrdersController>>();
        _controller = new OrdersController(_mockOrderService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateOrder_WithValidRequest_ReturnsAcceptedResult()
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
                }
            }
        };

        var expectedResponse = new OrderResponse
        {
            OrderId = "ORD-20241201-ABC123",
            Status = OrderStatus.Received,
            Message = "Order received and queued for processing",
            CreatedAt = DateTime.UtcNow
        };

        _mockOrderService
            .Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        result.Should().NotBeNull();
        var acceptedResult = result.Result.Should().BeOfType<AcceptedResult>().Subject;
        var responseValue = acceptedResult.Value.Should().BeOfType<OrderResponse>().Subject;
        
        responseValue.OrderId.Should().Be(expectedResponse.OrderId);
        responseValue.Status.Should().Be(OrderStatus.Received);
        
        _mockOrderService.Verify(x => x.CreateOrderAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_WithValidationException_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateOrderRequest
        {
            CustomerId = "",
            Items = new List<CreateOrderItemRequest>()
        };

        _mockOrderService
            .Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Validation failed"));

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        result.Should().NotBeNull();
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problemDetails = badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        
        problemDetails.Title.Should().Be("Validation Failed");
        problemDetails.Status.Should().Be(400);
    }

    [Fact]
    public async Task CreateOrder_WithUnexpectedException_ReturnsInternalServerError()
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

        _mockOrderService
            .Setup(x => x.CreateOrderAsync(It.IsAny<CreateOrderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await _controller.CreateOrder(request);

        // Assert
        result.Should().NotBeNull();
        var errorResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        errorResult.StatusCode.Should().Be(500);
        
        var problemDetails = errorResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Title.Should().Be("Internal Server Error");
    }

    [Fact]
    public async Task GetOrderStatus_WithExistingOrder_ReturnsOkResult()
    {
        // Arrange
        var orderId = "ORD-20241201-ABC123";
        var expectedResponse = new OrderResponse
        {
            OrderId = orderId,
            Status = OrderStatus.Processing,
            Message = "Order is being processed",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockOrderService
            .Setup(x => x.GetOrderStatusAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetOrderStatus(orderId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var responseValue = okResult.Value.Should().BeOfType<OrderResponse>().Subject;
        
        responseValue.OrderId.Should().Be(orderId);
        responseValue.Status.Should().Be(OrderStatus.Processing);
        
        _mockOrderService.Verify(x => x.GetOrderStatusAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrderStatus_WithNonExistentOrder_ReturnsNotFound()
    {
        // Arrange
        var orderId = "NON-EXISTENT";

        _mockOrderService
            .Setup(x => x.GetOrderStatusAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderResponse?)null);

        // Act
        var result = await _controller.GetOrderStatus(orderId);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        
        problemDetails.Title.Should().Be("Order Not Found");
        problemDetails.Status.Should().Be(404);
    }

    [Fact]
    public async Task CancelOrder_WithCancellableOrder_ReturnsOkResult()
    {
        // Arrange
        var orderId = "ORD-20241201-ABC123";
        var expectedResponse = new OrderResponse
        {
            OrderId = orderId,
            Status = OrderStatus.Cancelled,
            Message = "Order cancelled successfully",
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        _mockOrderService
            .Setup(x => x.CancelOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.CancelOrder(orderId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var responseValue = okResult.Value.Should().BeOfType<OrderResponse>().Subject;
        
        responseValue.OrderId.Should().Be(orderId);
        responseValue.Status.Should().Be(OrderStatus.Cancelled);
        
        _mockOrderService.Verify(x => x.CancelOrderAsync(orderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelOrder_WithNonCancellableOrder_ReturnsConflict()
    {
        // Arrange
        var orderId = "ORD-20241201-ABC123";

        _mockOrderService
            .Setup(x => x.CancelOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Order cannot be cancelled in current state"));

        // Act
        var result = await _controller.CancelOrder(orderId);

        // Assert
        result.Should().NotBeNull();
        var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problemDetails = conflictResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        
        problemDetails.Title.Should().Be("Cannot Cancel Order");
        problemDetails.Status.Should().Be(409);
    }

    [Fact]
    public async Task CancelOrder_WithNonExistentOrder_ReturnsNotFound()
    {
        // Arrange
        var orderId = "NON-EXISTENT";

        _mockOrderService
            .Setup(x => x.CancelOrderAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderResponse?)null);

        // Act
        var result = await _controller.CancelOrder(orderId);

        // Assert
        result.Should().NotBeNull();
        var notFoundResult = result.Result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problemDetails = notFoundResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        
        problemDetails.Title.Should().Be("Order Not Found");
        problemDetails.Status.Should().Be(404);
    }

    [Fact]
    public void HealthCheck_ReturnsOkResult()
    {
        // Act
        var result = _controller.HealthCheck();

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        
        okResult.Value.Should().NotBeNull();
        var healthResponse = okResult.Value as dynamic;
        healthResponse.Should().NotBeNull();
    }
}
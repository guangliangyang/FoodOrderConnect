using BidOne.ExternalOrderApi.Services;
using BidOne.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BidOne.ExternalOrderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderService orderService, ILogger<OrdersController> logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new order
    /// </summary>
    /// <param name="request">Order creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order creation response</returns>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Received order creation request for customer {CustomerId} with {ItemCount} items",
                request.CustomerId, request.Items.Count);

            var response = await _orderService.CreateOrderAsync(request, cancellationToken);

            _logger.LogInformation("Order {OrderId} created successfully with status {Status}",
                response.OrderId, response.Status);

            return Accepted(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed for order creation request from customer {CustomerId}",
                request.CustomerId);

            var problemDetails = new ValidationProblemDetails
            {
                Title = "Validation Failed",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            };

            return BadRequest(problemDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while creating order for customer {CustomerId}",
                request.CustomerId);

            var problemDetails = new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while processing your request",
                Status = StatusCodes.Status500InternalServerError
            };

            return StatusCode(StatusCodes.Status500InternalServerError, problemDetails);
        }
    }

    /// <summary>
    /// Get order status by ID
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order status information</returns>
    [HttpGet("{orderId}/status")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetOrderStatus(
        [FromRoute] string orderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await _orderService.GetOrderStatusAsync(orderId, cancellationToken);

            if (status == null)
            {
                _logger.LogWarning("Order {OrderId} not found", orderId);
                return NotFound(new ProblemDetails
                {
                    Title = "Order Not Found",
                    Detail = $"Order with ID '{orderId}' was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving status for order {OrderId}", orderId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while retrieving the order status",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Cancel an order
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cancellation result</returns>
    [HttpDelete("{orderId}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> CancelOrder(
        [FromRoute] string orderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _orderService.CancelOrderAsync(orderId, cancellationToken);

            if (result == null)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Order Not Found",
                    Detail = $"Order with ID '{orderId}' was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            _logger.LogInformation("Order {OrderId} cancelled successfully", orderId);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot cancel order {OrderId} - invalid state", orderId);

            return Conflict(new ProblemDetails
            {
                Title = "Cannot Cancel Order",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while cancelling the order",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult HealthCheck()
    {
        return Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
    }
}
using System.ComponentModel.DataAnnotations;
using BidOne.InternalSystemApi.Services;
using BidOne.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidOne.InternalSystemApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderProcessingService _orderProcessingService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IOrderProcessingService orderProcessingService, ILogger<OrdersController> logger)
    {
        _orderProcessingService = orderProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Process an order from the integration workflow
    /// </summary>
    /// <param name="request">Order processing request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order processing response</returns>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<OrderResponse>> ProcessOrder(
        [FromBody] ProcessOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing order {OrderId} for customer {CustomerId}",
                request.Order.Id, request.Order.CustomerId);

            var response = await _orderProcessingService.ProcessOrderAsync(request, cancellationToken);

            _logger.LogInformation("Order {OrderId} processed with status {Status}",
                response.OrderId, response.Status);

            return Ok(response);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed for order processing request {OrderId}",
                request.Order.Id);

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
            _logger.LogError(ex, "Unexpected error occurred while processing order {OrderId}",
                request.Order.Id);

            var problemDetails = new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred while processing the order",
                Status = StatusCodes.Status500InternalServerError
            };

            return StatusCode(StatusCodes.Status500InternalServerError, problemDetails);
        }
    }

    /// <summary>
    /// Get order details by ID
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details</returns>
    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> GetOrder(
        [FromRoute] string orderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var order = await _orderProcessingService.GetOrderAsync(orderId, cancellationToken);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found", orderId);
                return NotFound(new ProblemDetails
                {
                    Title = "Order Not Found",
                    Detail = $"Order with ID '{orderId}' was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order {OrderId}", orderId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while retrieving the order",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Update order status
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="request">Status update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated order response</returns>
    [HttpPatch("{orderId}/status")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponse>> UpdateOrderStatus(
        [FromRoute] string orderId,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _orderProcessingService.UpdateOrderStatusAsync(
                orderId, request.Status, request.Notes, cancellationToken);

            _logger.LogInformation("Order {OrderId} status updated to {Status}", orderId, request.Status);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot update status for order {OrderId}", orderId);

            return NotFound(new ProblemDetails
            {
                Title = "Order Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for order {OrderId}", orderId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while updating the order status",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Cancel an order
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="request">Cancellation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cancellation result</returns>
    [HttpDelete("{orderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelOrder(
        [FromRoute] string orderId,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _orderProcessingService.CancelOrderAsync(orderId, request.Reason, cancellationToken);

            if (!success)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Order Not Found",
                    Detail = $"Order with ID '{orderId}' was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            _logger.LogInformation("Order {OrderId} cancelled. Reason: {Reason}", orderId, request.Reason);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot cancel order {OrderId}", orderId);

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
    /// Get orders by customer
    /// </summary>
    /// <param name="customerId">Customer identifier</param>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of orders</returns>
    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IEnumerable<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetOrdersByCustomer(
        [FromRoute] string customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _orderProcessingService.GetOrdersByCustomerAsync(
                customerId, page, pageSize, cancellationToken);

            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for customer {CustomerId}", customerId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while retrieving customer orders",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get orders by supplier
    /// </summary>
    /// <param name="supplierId">Supplier identifier</param>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of orders</returns>
    [HttpGet("supplier/{supplierId}")]
    [ProducesResponseType(typeof(IEnumerable<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetOrdersBySupplier(
        [FromRoute] string supplierId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var orders = await _orderProcessingService.GetOrdersBySupplierAsync(
                supplierId, page, pageSize, cancellationToken);

            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for supplier {SupplierId}", supplierId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while retrieving supplier orders",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}

public class UpdateOrderStatusRequest
{
    [Required]
    public OrderStatus Status { get; set; }

    public string? Notes { get; set; }
}

public class CancelOrderRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;
using BidOne.InternalSystemApi.Mappings;
using BidOne.InternalSystemApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BidOne.InternalSystemApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(IInventoryService inventoryService, ILogger<InventoryController> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    /// <summary>
    /// Get inventory for a specific product
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Inventory information</returns>
    [HttpGet("{productId}")]
    [ProducesResponseType(typeof(Inventory), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Inventory>> GetInventory(
        [FromRoute] string productId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var inventory = await _inventoryService.GetInventoryAsync(productId, cancellationToken);

            if (inventory == null)
            {
                _logger.LogWarning("Inventory not found for product {ProductId}", productId);
                return NotFound(new ProblemDetails
                {
                    Title = "Inventory Not Found",
                    Detail = $"Inventory for product '{productId}' was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(inventory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving inventory for product {ProductId}", productId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while retrieving inventory information",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get all low stock items
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of low stock items</returns>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(IEnumerable<Inventory>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Inventory>>> GetLowStockItems(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var lowStockItems = await _inventoryService.GetLowStockItemsAsync(cancellationToken);
            return Ok(lowStockItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving low stock items");

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while retrieving low stock items",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Update inventory for a product
    /// </summary>
    /// <param name="productId">Product identifier</param>
    /// <param name="request">Inventory update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success or failure result</returns>
    [HttpPatch("{productId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateInventory(
        [FromRoute] string productId,
        [FromBody] UpdateInventoryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _inventoryService.UpdateInventoryAsync(
                productId, request.QuantityChange, request.Reason, cancellationToken);

            if (!success)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Product Not Found",
                    Detail = $"Product '{productId}' was not found in inventory",
                    Status = StatusCodes.Status404NotFound
                });
            }

            _logger.LogInformation("Inventory updated for product {ProductId}. Change: {Change}, Reason: {Reason}",
                productId, request.QuantityChange, request.Reason);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating inventory for product {ProductId}", productId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while updating inventory",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Reserve inventory for multiple products
    /// </summary>
    /// <param name="request">Inventory reservation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Reservation result</returns>
    [HttpPost("reserve")]
    [ProducesResponseType(typeof(InventoryReservationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<InventoryReservationResult>> ReserveInventory(
        [FromBody] BulkInventoryReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var reservationRequests = request.Items.Select(item => new InventoryReservationRequest
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                OrderId = request.OrderId
            }).ToList();

            var result = await _inventoryService.ReserveInventoryAsync(reservationRequests, cancellationToken);

            if (result.IsSuccessful)
            {
                _logger.LogInformation("Successfully reserved inventory for order {OrderId}", request.OrderId);
            }
            else
            {
                _logger.LogWarning("Failed to reserve inventory for order {OrderId}: {Reason}",
                    request.OrderId, result.FailureReason);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving inventory for order {OrderId}", request.OrderId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while reserving inventory",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Release inventory reservations for an order
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Release result</returns>
    [HttpDelete("reserve/{orderId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReleaseReservation(
        [FromRoute] string orderId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _inventoryService.ReleaseReservationAsync(orderId, cancellationToken);

            if (!success)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Order Not Found",
                    Detail = $"Order '{orderId}' was not found",
                    Status = StatusCodes.Status404NotFound
                });
            }

            _logger.LogInformation("Successfully released inventory reservations for order {OrderId}", orderId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing inventory reservations for order {OrderId}", orderId);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while releasing inventory reservations",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}

public class UpdateInventoryRequest
{
    [Required]
    public int QuantityChange { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;
}

public class BulkInventoryReservationRequest
{
    [Required]
    public string OrderId { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<InventoryReservationItem> Items { get; set; } = new();
}

public class InventoryReservationItem
{
    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

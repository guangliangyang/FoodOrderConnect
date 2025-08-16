using AutoMapper;
using BidOne.InternalSystemApi.Data;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.Shared.Events;
using BidOne.Shared.Models;
using BidOne.Shared.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BidOne.InternalSystemApi.Services;

public class OrderProcessingService : IOrderProcessingService
{
    private readonly BidOneDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IInventoryService _inventoryService;
    private readonly ISupplierNotificationService _supplierNotificationService;
    private readonly ILogger<OrderProcessingService> _logger;

    public OrderProcessingService(
        BidOneDbContext dbContext,
        IMapper mapper,
        IMessagePublisher messagePublisher,
        IInventoryService inventoryService,
        ISupplierNotificationService supplierNotificationService,
        ILogger<OrderProcessingService> logger)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _messagePublisher = messagePublisher;
        _inventoryService = inventoryService;
        _supplierNotificationService = supplierNotificationService;
        _logger = logger;
    }

    public async Task<OrderResponse> ProcessOrderAsync(ProcessOrderRequest request, CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            _logger.LogInformation("Processing order {OrderId} for customer {CustomerId}", 
                request.Order.Id, request.Order.CustomerId);

            // Check if order already exists
            var existingOrder = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == request.Order.Id, cancellationToken);

            OrderEntity orderEntity;
            
            if (existingOrder == null)
            {
                // Create new order
                orderEntity = _mapper.Map<OrderEntity>(request);
                orderEntity.Id = request.Order.Id;
                orderEntity.Status = OrderStatus.Processing;
                orderEntity.UpdatedAt = DateTime.UtcNow;

                // Map order items
                orderEntity.Items = request.Order.Items.Select(item =>
                {
                    var itemEntity = _mapper.Map<OrderItemEntity>(item);
                    itemEntity.Id = Guid.NewGuid();
                    itemEntity.OrderId = orderEntity.Id;
                    itemEntity.TotalPrice = item.Quantity * item.UnitPrice;
                    return itemEntity;
                }).ToList();

                // Calculate total amount
                orderEntity.TotalAmount = orderEntity.Items.Sum(i => i.TotalPrice);

                await _dbContext.Orders.AddAsync(orderEntity, cancellationToken);
            }
            else
            {
                // Update existing order
                _mapper.Map(request, existingOrder);
                existingOrder.Status = OrderStatus.Processing;
                existingOrder.UpdatedAt = DateTime.UtcNow;
                orderEntity = existingOrder;
            }

            // Reserve inventory
            var reservationResult = await _inventoryService.ReserveInventoryAsync(
                orderEntity.Items.Select(i => new InventoryReservationRequest
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    OrderId = orderEntity.Id
                }).ToList(), cancellationToken);

            if (!reservationResult.IsSuccessful)
            {
                _logger.LogWarning("Inventory reservation failed for order {OrderId}: {Reason}", 
                    orderEntity.Id, reservationResult.FailureReason);
                
                orderEntity.Status = OrderStatus.Failed;
                orderEntity.Metadata["FailureReason"] = reservationResult.FailureReason ?? "Inventory reservation failed";
                
                await SaveOrderEvent(orderEntity.Id, "InventoryReservationFailed", reservationResult, cancellationToken);
            }
            else
            {
                // Determine supplier and assign
                var supplier = await DetermineSupplierAsync(orderEntity, cancellationToken);
                if (supplier != null)
                {
                    orderEntity.SupplierId = supplier.Id;
                    orderEntity.Status = OrderStatus.Confirmed;
                    orderEntity.ConfirmedAt = DateTime.UtcNow;

                    // Notify supplier
                    await _supplierNotificationService.NotifyOrderAsync(supplier, orderEntity, cancellationToken);
                    await SaveOrderEvent(orderEntity.Id, "SupplierNotified", new { SupplierId = supplier.Id }, cancellationToken);
                }
                else
                {
                    orderEntity.Status = OrderStatus.Failed;
                    orderEntity.Metadata["FailureReason"] = "No suitable supplier found";
                    await SaveOrderEvent(orderEntity.Id, "NoSupplierFound", null, cancellationToken);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Publish order confirmed or failed event
            if (orderEntity.Status == OrderStatus.Confirmed)
            {
                var confirmedEvent = new OrderConfirmedEvent
                {
                    OrderId = orderEntity.Id,
                    ConfirmationId = Guid.NewGuid().ToString(),
                    TotalAmount = orderEntity.TotalAmount,
                    ConfirmedAt = orderEntity.ConfirmedAt!.Value,
                    ProcessedBy = "InternalSystemApi",
                    CorrelationId = orderEntity.Metadata.GetValueOrDefault("CorrelationId", string.Empty).ToString() ?? string.Empty
                };

                await _messagePublisher.PublishEventAsync(confirmedEvent, cancellationToken);
            }
            else
            {
                var failedEvent = new OrderFailedEvent
                {
                    OrderId = orderEntity.Id,
                    FailureReason = orderEntity.Metadata.GetValueOrDefault("FailureReason", "Unknown error").ToString() ?? "Unknown error",
                    FailedAt = DateTime.UtcNow,
                    IsRetryable = false,
                    CorrelationId = orderEntity.Metadata.GetValueOrDefault("CorrelationId", string.Empty).ToString() ?? string.Empty
                };

                await _messagePublisher.PublishEventAsync(failedEvent, cancellationToken);
            }

            _logger.LogInformation("Order {OrderId} processed successfully with status {Status}", 
                orderEntity.Id, orderEntity.Status);

            return new OrderResponse
            {
                OrderId = orderEntity.Id,
                Status = orderEntity.Status,
                Message = GetStatusMessage(orderEntity.Status),
                CreatedAt = orderEntity.CreatedAt
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to process order {OrderId}", request.Order.Id);
            throw;
        }
    }

    public async Task<OrderResponse?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .Include(o => o.Customer)
            .Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order == null)
            return null;

        return new OrderResponse
        {
            OrderId = order.Id,
            Status = order.Status,
            Message = GetStatusMessage(order.Status),
            CreatedAt = order.CreatedAt
        };
    }

    public async Task<OrderResponse> UpdateOrderStatusAsync(string orderId, OrderStatus status, string? notes = null, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        
        if (order == null)
            throw new InvalidOperationException($"Order {orderId} not found");

        var previousStatus = order.Status;
        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(notes))
        {
            order.Notes = notes;
        }

        await SaveOrderEvent(orderId, "StatusChanged", new { PreviousStatus = previousStatus, NewStatus = status, Notes = notes }, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Order {OrderId} status updated from {PreviousStatus} to {NewStatus}", 
            orderId, previousStatus, status);

        return new OrderResponse
        {
            OrderId = order.Id,
            Status = order.Status,
            Message = GetStatusMessage(order.Status),
            CreatedAt = order.CreatedAt
        };
    }

    public async Task<bool> CancelOrderAsync(string orderId, string reason, CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            var order = await _dbContext.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order == null)
                return false;

            if (order.Status is OrderStatus.Confirmed or OrderStatus.Delivered)
            {
                throw new InvalidOperationException($"Cannot cancel order {orderId} in status {order.Status}");
            }

            // Release reserved inventory
            if (order.Status == OrderStatus.Processing)
            {
                await _inventoryService.ReleaseReservationAsync(orderId, cancellationToken);
            }

            order.Status = OrderStatus.Cancelled;
            order.UpdatedAt = DateTime.UtcNow;
            order.Metadata["CancellationReason"] = reason;
            order.Metadata["CancelledAt"] = DateTime.UtcNow;

            await SaveOrderEvent(orderId, "OrderCancelled", new { Reason = reason }, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} cancelled. Reason: {Reason}", orderId, reason);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to cancel order {OrderId}", orderId);
            throw;
        }
    }

    public async Task<IEnumerable<OrderResponse>> GetOrdersByCustomerAsync(string customerId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var orders = await _dbContext.Orders
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderResponse
            {
                OrderId = o.Id,
                Status = o.Status,
                Message = GetStatusMessage(o.Status),
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return orders;
    }

    public async Task<IEnumerable<OrderResponse>> GetOrdersBySupplierAsync(string supplierId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var orders = await _dbContext.Orders
            .Where(o => o.SupplierId == supplierId)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderResponse
            {
                OrderId = o.Id,
                Status = o.Status,
                Message = GetStatusMessage(o.Status),
                CreatedAt = o.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return orders;
    }

    private async Task<SupplierEntity?> DetermineSupplierAsync(OrderEntity order, CancellationToken cancellationToken)
    {
        // Simple supplier determination logic - can be enhanced with more sophisticated rules
        var productIds = order.Items.Select(i => i.ProductId).ToList();
        
        var supplier = await _dbContext.Suppliers
            .Where(s => s.IsActive && s.Products.Any(p => productIds.Contains(p.Id)))
            .FirstOrDefaultAsync(cancellationToken);

        return supplier;
    }

    private async Task SaveOrderEvent(string orderId, string eventType, object? eventData, CancellationToken cancellationToken)
    {
        var orderEvent = new OrderEventEntity
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            EventType = eventType,
            EventData = eventData != null ? JsonSerializer.Serialize(eventData) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "System"
        };

        await _dbContext.OrderEvents.AddAsync(orderEvent, cancellationToken);
    }

    private static string GetStatusMessage(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Received => "Order received and queued for processing",
            OrderStatus.Validating => "Order is being validated",
            OrderStatus.Validated => "Order validation completed",
            OrderStatus.Enriching => "Order data is being enriched",
            OrderStatus.Enriched => "Order data enrichment completed",
            OrderStatus.Processing => "Order is being processed",
            OrderStatus.Confirmed => "Order confirmed and sent to supplier",
            OrderStatus.Failed => "Order processing failed",
            OrderStatus.Cancelled => "Order was cancelled",
            OrderStatus.Delivered => "Order has been delivered",
            _ => "Unknown status"
        };
    }
}

public class InventoryReservationRequest
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string OrderId { get; set; } = string.Empty;
}

public class InventoryReservationResult
{
    public bool IsSuccessful { get; set; }
    public string? FailureReason { get; set; }
    public List<string> UnavailableProducts { get; set; } = new();
}
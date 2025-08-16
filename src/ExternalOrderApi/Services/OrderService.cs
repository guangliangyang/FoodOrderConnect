using BidOne.Shared.Events;
using BidOne.Shared.Models;
using BidOne.Shared.Services;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace BidOne.ExternalOrderApi.Services;

public class OrderService : IOrderService
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly IDistributedCache _cache;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IMessagePublisher messagePublisher,
        IDistributedCache cache,
        ILogger<OrderService> logger)
    {
        _messagePublisher = messagePublisher;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var orderId = GenerateOrderId();
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            // Create order object
            var order = new Order
            {
                Id = orderId,
                CustomerId = request.CustomerId,
                Items = request.Items.Select(item => new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList(),
                Status = OrderStatus.Received,
                CreatedAt = DateTime.UtcNow,
                DeliveryDate = request.DeliveryDate,
                Notes = request.Notes,
                Metadata = new Dictionary<string, object>
                {
                    ["SourceSystem"] = "ExternalOrderApi",
                    ["CorrelationId"] = correlationId,
                    ["ClientIpAddress"] = GetClientIpAddress(),
                    ["UserAgent"] = GetUserAgent()
                }
            };

            // Calculate total amount
            order.TotalAmount = order.Items.Sum(item => item.TotalPrice);

            // Store order in cache for quick status lookups
            await CacheOrderAsync(order, cancellationToken);

            // Publish order received event to Service Bus
            var orderReceivedEvent = new OrderReceivedEvent
            {
                OrderId = orderId,
                CustomerId = request.CustomerId,
                ReceivedAt = order.CreatedAt,
                SourceSystem = "ExternalOrderApi",
                CorrelationId = correlationId
            };

            await _messagePublisher.PublishAsync(order, "order-received", cancellationToken);
            await _messagePublisher.PublishEventAsync(orderReceivedEvent, cancellationToken);

            _logger.LogInformation("Order {OrderId} created and published successfully. CorrelationId: {CorrelationId}",
                orderId, correlationId);

            return new OrderResponse
            {
                OrderId = orderId,
                Status = OrderStatus.Received,
                Message = "Order received and queued for processing",
                CreatedAt = order.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order {OrderId}. CorrelationId: {CorrelationId}",
                orderId, correlationId);
            throw;
        }
    }

    public async Task<OrderResponse?> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetOrderCacheKey(orderId);
            var cachedOrderJson = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedOrderJson == null)
            {
                _logger.LogWarning("Order {OrderId} not found in cache", orderId);
                return null;
            }

            var order = JsonSerializer.Deserialize<Order>(cachedOrderJson);
            if (order == null)
            {
                _logger.LogWarning("Failed to deserialize order {OrderId} from cache", orderId);
                return null;
            }

            return new OrderResponse
            {
                OrderId = order.Id,
                Status = order.Status,
                Message = GetStatusMessage(order.Status),
                CreatedAt = order.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order status for {OrderId}", orderId);
            throw;
        }
    }

    public async Task<OrderResponse?> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = GetOrderCacheKey(orderId);
            var cachedOrderJson = await _cache.GetStringAsync(cacheKey, cancellationToken);

            if (cachedOrderJson == null)
            {
                _logger.LogWarning("Order {OrderId} not found for cancellation", orderId);
                return null;
            }

            var order = JsonSerializer.Deserialize<Order>(cachedOrderJson);
            if (order == null)
            {
                _logger.LogWarning("Failed to deserialize order {OrderId} for cancellation", orderId);
                return null;
            }

            // Check if order can be cancelled
            if (!CanCancelOrder(order.Status))
            {
                throw new InvalidOperationException($"Order {orderId} cannot be cancelled in status {order.Status}");
            }

            // Update order status
            order.Status = OrderStatus.Cancelled;
            await CacheOrderAsync(order, cancellationToken);

            // Publish cancellation event
            var orderCancelledEvent = new OrderFailedEvent
            {
                OrderId = orderId,
                FailureReason = "Cancelled by user",
                FailedAt = DateTime.UtcNow,
                IsRetryable = false,
                CorrelationId = order.Metadata.GetValueOrDefault("CorrelationId", string.Empty).ToString() ?? string.Empty
            };

            await _messagePublisher.PublishEventAsync(orderCancelledEvent, cancellationToken);

            _logger.LogInformation("Order {OrderId} cancelled successfully", orderId);

            return new OrderResponse
            {
                OrderId = orderId,
                Status = OrderStatus.Cancelled,
                Message = "Order cancelled successfully",
                CreatedAt = order.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
            throw;
        }
    }

    private async Task CacheOrderAsync(Order order, CancellationToken cancellationToken)
    {
        var cacheKey = GetOrderCacheKey(order.Id);
        var cacheOptions = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(24),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
        };

        var orderJson = JsonSerializer.Serialize(order, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _cache.SetStringAsync(cacheKey, orderJson, cacheOptions, cancellationToken);
    }

    private static string GenerateOrderId()
    {
        return $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    private static string GetOrderCacheKey(string orderId)
    {
        return $"order:{orderId}";
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

    private static bool CanCancelOrder(OrderStatus status)
    {
        return status is OrderStatus.Received or OrderStatus.Validating or OrderStatus.Validated;
    }

    private string GetClientIpAddress()
    {
        // This would typically be injected via IHttpContextAccessor
        return "127.0.0.1"; // Placeholder
    }

    private string GetUserAgent()
    {
        // This would typically be injected via IHttpContextAccessor
        return "Unknown"; // Placeholder
    }
}
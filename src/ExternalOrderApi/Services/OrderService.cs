using System.Diagnostics;
using System.Text.Json;
using BidOne.Shared.Domain.ValueObjects;
using BidOne.Shared.Events;
using BidOne.Shared.Metrics;
using BidOne.Shared.Models;
using BidOne.Shared.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace BidOne.ExternalOrderApi.Services;

public class OrderService : IOrderService
{
    private readonly IMessagePublisher _messagePublisher;
    private readonly IDashboardEventPublisher _dashboardEventPublisher;
    private readonly IDistributedCache _cache;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IMessagePublisher messagePublisher,
        IDashboardEventPublisher dashboardEventPublisher,
        IDistributedCache cache,
        ILogger<OrderService> logger)
    {
        _messagePublisher = messagePublisher;
        _dashboardEventPublisher = dashboardEventPublisher;
        _cache = cache;
        _logger = logger;
    }

    public async Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        var orderId = GenerateOrderId();
        var correlationId = Guid.NewGuid().ToString();

        // 📊 开始监控订单处理时间
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Create order using domain model
            var order = Order.Create(OrderId.Create(orderId), CustomerId.Create(request.CustomerId));

            // Add items using domain methods
            foreach (var item in request.Items)
            {
                var productInfo = ProductInfo.Create(item.ProductId, item.ProductId); // ProductName would come from enrichment
                var quantity = Quantity.Create(item.Quantity);
                var unitPrice = Money.Create(item.UnitPrice);

                order.AddItem(productInfo, quantity, unitPrice);
            }

            // Set delivery and notes
            order.UpdateDeliveryInfo(request.DeliveryDate, null);
            order.SetNotes(request.Notes);

            // Set metadata
            order.Metadata["SourceSystem"] = "ExternalOrderApi";
            order.Metadata["CorrelationId"] = correlationId;
            order.Metadata["ClientIpAddress"] = GetClientIpAddress();
            order.Metadata["UserAgent"] = GetUserAgent();

            // Store order in cache for quick status lookups
            await CacheOrderAsync(order, cancellationToken);

            // Publish order received event to Service Bus
            var orderReceivedEvent = new OrderReceivedEvent
            {
                OrderId = order.Id.Value,
                CustomerId = order.CustomerId.Value,
                ReceivedAt = order.CreatedAt,
                SourceSystem = "ExternalOrderApi",
                CorrelationId = correlationId
            };

            await _messagePublisher.PublishAsync(order, "order-received", cancellationToken);
            await _messagePublisher.PublishEventAsync(orderReceivedEvent, cancellationToken);

            _logger.LogInformation("Order {OrderId} created and published successfully. CorrelationId: {CorrelationId}",
                orderId, correlationId);

            // 📊 记录业务指标
            BusinessMetrics.OrdersProcessed.WithLabels("received", "ExternalOrderApi").Inc();
            BusinessMetrics.PendingOrders.WithLabels("ExternalOrderApi").Inc();
            BusinessMetrics.OrderProcessingTime.WithLabels("ExternalOrderApi", "CreateOrder")
                .Observe(stopwatch.Elapsed.TotalSeconds);

            // 🎯 发布仪表板指标事件 (轻量级，不影响主流程)
            _ = Task.Run(async () =>
            {
                try
                {
                    await PublishDashboardMetricsAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to publish dashboard metrics, but order processing continues normally");
                }
            }, cancellationToken);

            return new OrderResponse
            {
                OrderId = order.Id.Value,
                Status = order.Status,
                Message = "Order received and queued for processing",
                CreatedAt = order.CreatedAt
            };
        }
        catch (Exception ex)
        {
            // 📊 记录失败指标
            BusinessMetrics.OrdersProcessed.WithLabels("failed", "ExternalOrderApi").Inc();

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
                OrderId = order.Id.Value,
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

            // Check if order can be cancelled using domain logic
            if (!order.CanBeCancelled())
            {
                throw new InvalidOperationException($"Order {orderId} cannot be cancelled in status {order.Status}");
            }

            // Cancel order using domain method
            order.Cancel("Cancelled by user");
            await CacheOrderAsync(order, cancellationToken);

            // Publish cancellation event
            var orderCancelledEvent = new OrderFailedEvent
            {
                OrderId = order.Id.Value,
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

    /// <summary>
    /// 发布仪表板指标更新事件 (轻量级，异步执行)
    /// </summary>
    private async Task PublishDashboardMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 模拟从缓存或数据库获取当前订单统计数据
            var todayOrdersCount = await GetTodayOrdersCountFromCache();
            var totalOrdersCount = await GetTotalOrdersCountFromCache();
            var pendingOrdersCount = await GetPendingOrdersCountFromCache();

            // 发布订单指标更新事件到 Event Grid
            await _dashboardEventPublisher.PublishOrderMetricsAsync(
                totalOrders: totalOrdersCount,
                todayOrders: todayOrdersCount,
                pendingOrders: pendingOrdersCount,
                status: "Order Received",
                cancellationToken);

            _logger.LogDebug("📊 Dashboard order metrics published successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish dashboard metrics");
            // 不重新抛出异常，避免影响主业务流程
        }
    }

    /// <summary>
    /// 从缓存获取今日订单数 (模拟实现)
    /// </summary>
    private async Task<int> GetTodayOrdersCountFromCache()
    {
        try
        {
            var cacheKey = $"dashboard:orders:today:{DateTime.UtcNow:yyyy-MM-dd}";
            var countStr = await _cache.GetStringAsync(cacheKey);

            if (int.TryParse(countStr, out var count))
            {
                count++; // 新订单加 1
                await _cache.SetStringAsync(cacheKey, count.ToString(),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) });
                return count;
            }
            else
            {
                // 首次设置为 1
                await _cache.SetStringAsync(cacheKey, "1",
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) });
                return 1;
            }
        }
        catch
        {
            return 1; // 默认值
        }
    }

    /// <summary>
    /// 从缓存获取总订单数 (模拟实现)
    /// </summary>
    private async Task<int> GetTotalOrdersCountFromCache()
    {
        try
        {
            var cacheKey = "dashboard:orders:total";
            var countStr = await _cache.GetStringAsync(cacheKey);

            if (int.TryParse(countStr, out var count))
            {
                count++; // 新订单加 1
                await _cache.SetStringAsync(cacheKey, count.ToString());
                return count;
            }
            else
            {
                // 首次设置为 1
                await _cache.SetStringAsync(cacheKey, "1");
                return 1;
            }
        }
        catch
        {
            return 1; // 默认值
        }
    }

    /// <summary>
    /// 从缓存获取待处理订单数 (模拟实现)
    /// </summary>
    private async Task<int> GetPendingOrdersCountFromCache()
    {
        try
        {
            var cacheKey = "dashboard:orders:pending";
            var countStr = await _cache.GetStringAsync(cacheKey);

            if (int.TryParse(countStr, out var count))
            {
                count++; // 新待处理订单加 1
                await _cache.SetStringAsync(cacheKey, count.ToString());
                return count;
            }
            else
            {
                // 首次设置为 1
                await _cache.SetStringAsync(cacheKey, "1");
                return 1;
            }
        }
        catch
        {
            return 1; // 默认值
        }
    }
}

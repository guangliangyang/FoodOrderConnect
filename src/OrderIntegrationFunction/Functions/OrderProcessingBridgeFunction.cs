using System.Diagnostics;
using System.Text.Json;
using BidOne.OrderIntegrationFunction.Services;
using BidOne.Shared.Events;
using BidOne.Shared.Metrics;
using BidOne.Shared.Models;
using BidOne.Shared.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BidOne.OrderIntegrationFunction.Functions;

/// <summary>
/// Bridge function that consumes order-processing messages and forwards them to Internal System API
/// This completes the integration between the message-driven Functions architecture and the HTTP-based Internal API
/// </summary>
public class OrderProcessingBridgeFunction
{
    private readonly IInternalApiClient _internalApiClient;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<OrderProcessingBridgeFunction> _logger;

    public OrderProcessingBridgeFunction(
        IInternalApiClient internalApiClient,
        IMessagePublisher messagePublisher,
        ILogger<OrderProcessingBridgeFunction> logger)
    {
        _internalApiClient = internalApiClient;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    /// <summary>
    /// Main bridge function that processes orders from the order-processing queue
    /// and forwards them to the Internal System API via HTTP
    /// </summary>
    /// <param name="orderMessage">Serialized ProcessOrderRequest from order-processing queue</param>
    /// <returns>Task representing the async operation</returns>
    [Function("ProcessOrderBridge")]
    public async Task ProcessOrderBridge(
        [ServiceBusTrigger("order-processing", Connection = "ServiceBusConnection")] string orderMessage)
    {
        var correlationId = Guid.NewGuid().ToString();

        _logger.LogInformation("🔗 Order processing bridge triggered. CorrelationId: {CorrelationId}", correlationId);

        try
        {
            // Deserialize the order processing request
            var processingRequest = JsonSerializer.Deserialize<ProcessOrderRequest>(orderMessage, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (processingRequest?.Order == null)
            {
                _logger.LogError("Failed to deserialize ProcessOrderRequest from Service Bus message. CorrelationId: {CorrelationId}", correlationId);
                throw new InvalidOperationException("Invalid ProcessOrderRequest data in message");
            }

            var orderId = processingRequest.Order.Id;
            _logger.LogInformation("Processing order {OrderId} via Internal System API bridge. CorrelationId: {CorrelationId}",
                orderId, correlationId);

            // 📊 Start monitoring order processing time
            var stopwatch = Stopwatch.StartNew();

            // Validate that we can reach the Internal System API
            var isApiHealthy = await _internalApiClient.IsHealthyAsync();
            if (!isApiHealthy)
            {
                _logger.LogWarning("Internal System API health check failed for order {OrderId}. CorrelationId: {CorrelationId}",
                    orderId, correlationId);
                // Don't throw - let the retry mechanism handle this
            }

            // Forward the order to Internal System API
            var orderResponse = await _internalApiClient.ProcessOrderAsync(processingRequest);

            // 📊 Record processing time
            BusinessMetrics.OrderProcessingTime.WithLabels("OrderProcessingBridge", "ProcessOrder")
                .Observe(stopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation("Order {OrderId} successfully processed via Internal System API with status {Status}. CorrelationId: {CorrelationId}",
                orderId, orderResponse.Status, correlationId);

            // 📊 Record successful processing metrics
            BusinessMetrics.OrdersProcessed.WithLabels("confirmed", "OrderProcessingBridge").Inc();

            // Publish success event for monitoring and downstream systems
            await PublishOrderProcessedEvent(processingRequest.Order, orderResponse, correlationId);

            // If the order was confirmed, we can consider the full processing pipeline complete
            if (orderResponse.Status == OrderStatus.Confirmed)
            {
                _logger.LogInformation("🎉 Order {OrderId} processing pipeline completed successfully. Final status: {Status}. CorrelationId: {CorrelationId}",
                    orderId, orderResponse.Status, correlationId);
            }
        }
        catch (ArgumentException ex)
        {
            // Bad request - don't retry
            _logger.LogError(ex, "Invalid order data in processing request. This message will be moved to dead letter queue. CorrelationId: {CorrelationId}", correlationId);

            // 📊 Record validation failure
            BusinessMetrics.OrdersProcessed.WithLabels("failed_validation", "OrderProcessingBridge").Inc();

            throw; // This will move the message to dead letter queue
        }
        catch (UnauthorizedAccessException ex)
        {
            // Authentication issue - could be transient
            _logger.LogError(ex, "Authentication failed for Internal System API. This message will be retried. CorrelationId: {CorrelationId}", correlationId);

            // 📊 Record auth failure
            BusinessMetrics.OrdersProcessed.WithLabels("failed_auth", "OrderProcessingBridge").Inc();

            throw; // This will trigger retry
        }
        catch (TimeoutException ex)
        {
            // Timeout - likely transient, should retry
            _logger.LogError(ex, "Timeout calling Internal System API. This message will be retried. CorrelationId: {CorrelationId}", correlationId);

            // 📊 Record timeout failure
            BusinessMetrics.OrdersProcessed.WithLabels("failed_timeout", "OrderProcessingBridge").Inc();

            throw; // This will trigger retry
        }
        catch (HttpRequestException ex)
        {
            // HTTP error - could be transient
            _logger.LogError(ex, "HTTP error calling Internal System API. This message will be retried. CorrelationId: {CorrelationId}", correlationId);

            // 📊 Record HTTP failure
            BusinessMetrics.OrdersProcessed.WithLabels("failed_http", "OrderProcessingBridge").Inc();

            throw; // This will trigger retry
        }
        catch (Exception ex)
        {
            // Unexpected error
            _logger.LogError(ex, "Unexpected error processing order via Internal System API bridge. CorrelationId: {CorrelationId}", correlationId);

            // 📊 Record general failure
            BusinessMetrics.OrdersProcessed.WithLabels("failed_unexpected", "OrderProcessingBridge").Inc();

            throw; // This will trigger retry, then eventually dead letter
        }
    }

    /// <summary>
    /// Publish an event indicating that an order has been successfully processed by the Internal System API
    /// </summary>
    private async Task PublishOrderProcessedEvent(Order order, OrderResponse response, string correlationId)
    {
        try
        {
            var processedEvent = new OrderProcessedEvent
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                ProcessedAt = DateTime.UtcNow,
                FinalStatus = response.Status,
                ProcessingStage = "InternalSystemApi",
                Source = "OrderProcessingBridge",
                CorrelationId = correlationId,
                ProcessingData = new Dictionary<string, object>
                {
                    ["ApiResponseTime"] = DateTime.UtcNow,
                    ["FinalMessage"] = response.Message ?? string.Empty,
                    ["OrderValue"] = order.Items.Sum(i => i.TotalPrice),
                    ["ItemCount"] = order.Items.Count
                }
            };

            // Publish to a completion event queue for monitoring and potential downstream processing
            await _messagePublisher.PublishEventAsync(processedEvent);

            _logger.LogInformation("Order processed event published for order {OrderId}. CorrelationId: {CorrelationId}",
                order.Id, correlationId);
        }
        catch (Exception ex)
        {
            // Don't fail the main operation if event publishing fails
            _logger.LogWarning(ex, "Failed to publish order processed event for order {OrderId}. CorrelationId: {CorrelationId}",
                order.Id, correlationId);
        }
    }
}

/// <summary>
/// Event published when an order is successfully processed by the Internal System API
/// </summary>
public class OrderProcessedEvent : IntegrationEvent
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public OrderStatus FinalStatus { get; set; }
    public string ProcessingStage { get; set; } = string.Empty;
    public new string Source { get; set; } = string.Empty;
    public new string CorrelationId { get; set; } = string.Empty;
    public Dictionary<string, object> ProcessingData { get; set; } = new();
}

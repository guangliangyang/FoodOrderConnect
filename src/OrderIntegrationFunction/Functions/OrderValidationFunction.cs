using System.Text.Json;
using BidOne.OrderIntegrationFunction.Services;
using BidOne.Shared.Events;
using BidOne.Shared.Models;
using BidOne.Shared.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BidOne.OrderIntegrationFunction.Functions;

public class OrderValidationFunction
{
    private readonly IOrderValidationService _validationService;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<OrderValidationFunction> _logger;

    public OrderValidationFunction(
        IOrderValidationService validationService,
        IMessagePublisher messagePublisher,
        ILogger<OrderValidationFunction> logger)
    {
        _validationService = validationService;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    [Function("ValidateOrder")]
    public async Task<ValidationResult> ValidateOrder(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] Microsoft.Azure.Functions.Worker.Http.HttpRequestData req)
    {
        _logger.LogInformation("Order validation function triggered via HTTP");

        try
        {
            // Read request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonSerializer.Deserialize<Order>(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (order == null)
            {
                _logger.LogWarning("Invalid order data received");
                return new ValidationResult
                {
                    IsValid = false,
                    Errors = new List<ValidationError>
                    {
                        new()
                        {
                            Field = "Order",
                            Code = "INVALID_DATA",
                            Message = "Invalid order data format"
                        }
                    },
                    ValidatedAt = DateTime.UtcNow,
                    ValidatedBy = "OrderValidationFunction"
                };
            }

            _logger.LogInformation("Validating order {OrderId} for customer {CustomerId}",
                order.Id, order.CustomerId);

            var result = await _validationService.ValidateOrderAsync(order);

            _logger.LogInformation("Order {OrderId} validation completed. IsValid: {IsValid}, ErrorCount: {ErrorCount}",
                order.Id, result.IsValid, result.Errors.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during order validation");

            return new ValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new()
                    {
                        Field = "System",
                        Code = "VALIDATION_ERROR",
                        Message = "An error occurred during order validation"
                    }
                },
                ValidatedAt = DateTime.UtcNow,
                ValidatedBy = "OrderValidationFunction"
            };
        }
    }

    // 🔄 双处理架构说明: 这是处理路径1 (Azure Functions 链式处理)
    // 同时还有路径2 (Azure Logic Apps) 也在监听相同的 order-received 队列
    // 两条路径并行处理，用于演示不同的 Azure 集成模式:
    // - Functions 路径: 轻量级无服务器处理 (本函数所在路径)
    // - Logic Apps 路径: 可视化工作流编排
    [Function("ValidateOrderFromServiceBus")]
    [ServiceBusOutput("order-validated", Connection = "ServiceBusConnection")]
    public async Task<string> ValidateOrderFromServiceBus(
        [ServiceBusTrigger("order-received", Connection = "ServiceBusConnection")] string orderMessage)
    {
        _logger.LogInformation("Order validation function triggered from Service Bus");

        try
        {
            var order = JsonSerializer.Deserialize<Order>(orderMessage, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (order == null)
            {
                _logger.LogError("Failed to deserialize order from Service Bus message");
                throw new InvalidOperationException("Invalid order data");
            }

            _logger.LogInformation("Validating order {OrderId} from Service Bus", order.Id);

            var validationResult = await _validationService.ValidateOrderAsync(order);

            // 🎯 如果验证失败且是高价值错误，发布智能沟通事件
            if (!validationResult.IsValid && IsHighValueError(order, validationResult))
            {
                await PublishHighValueErrorEvent(order, validationResult);
            }

            // Create validation response
            var response = new OrderValidationResponse
            {
                Order = order,
                ValidationResult = validationResult,
                ProcessedAt = DateTime.UtcNow
            };

            var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Order {OrderId} validation result sent to order-validated queue", order.Id);
            return responseJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order validation from Service Bus");
            throw; // This will move the message to dead letter queue
        }
    }

    private static bool IsHighValueError(Order order, ValidationResult validationResult)
    {
        // 高价值错误条件：订单金额超过 $1000 或客户是重要客户
        var orderValue = order.Items.Sum(i => i.TotalPrice);
        var isHighValueOrder = orderValue > 1000m;

        // 检查是否为关键错误类型
        var criticalErrors = new[] { "CUSTOMER_NOT_FOUND", "PRODUCT_NOT_FOUND", "PRICE_MISMATCH", "ORDER_VALUE_EXCEEDED" };
        var hasCriticalError = validationResult.Errors.Any(e => criticalErrors.Contains(e.Code));

        return isHighValueOrder || hasCriticalError;
    }

    private async Task PublishHighValueErrorEvent(Order order, ValidationResult validationResult)
    {
        try
        {
            var errorEvent = new HighValueErrorEvent
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                CustomerEmail = order.CustomerEmail ?? "unknown@example.com",
                ErrorCategory = GetErrorCategory(validationResult.Errors),
                ErrorMessage = GetPrimaryErrorMessage(validationResult.Errors),
                TechnicalDetails = JsonSerializer.Serialize(validationResult.Errors),
                OrderValue = order.Items.Sum(i => i.TotalPrice),
                CustomerTier = GetCustomerTier(order),
                ErrorOccurredAt = DateTime.UtcNow,
                RetryCount = 0,
                ProcessingStage = "Validation",
                Source = "OrderValidationFunction",
                CorrelationId = order.Metadata.GetValueOrDefault("CorrelationId", string.Empty).ToString() ?? string.Empty,
                ContextData = new Dictionary<string, object>
                {
                    ["OrderItemCount"] = order.Items.Count,
                    ["ValidationErrorCount"] = validationResult.Errors.Count,
                    ["ValidationDuration"] = validationResult.ValidationData?.GetValueOrDefault("ValidationDuration", 0) ?? 0
                }
            };

            // 发布到专门的高价值错误队列
            await _messagePublisher.PublishAsync(errorEvent, "high-value-errors", CancellationToken.None);

            _logger.LogWarning("🚨 High-value error event published for order {OrderId}, value ${OrderValue:N2}",
                order.Id, errorEvent.OrderValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish high-value error event for order {OrderId}", order.Id);
        }
    }

    private static string GetErrorCategory(List<ValidationError> errors)
    {
        if (errors.Any(e => e.Code.Contains("CUSTOMER")))
            return "Customer";
        if (errors.Any(e => e.Code.Contains("PRODUCT")))
            return "Product";
        if (errors.Any(e => e.Code.Contains("PRICE")))
            return "Pricing";
        if (errors.Any(e => e.Code.Contains("DELIVERY")))
            return "Delivery";
        return "General";
    }

    private static string GetPrimaryErrorMessage(List<ValidationError> errors)
    {
        return errors.FirstOrDefault()?.Message ?? "Unknown validation error";
    }

    private static string GetCustomerTier(Order order)
    {
        // 模拟客户等级判断逻辑
        var orderValue = order.Items.Sum(i => i.TotalPrice);
        return orderValue switch
        {
            > 5000m => "Premium",
            > 2000m => "Gold",
            > 500m => "Silver",
            _ => "Standard"
        };
    }
}

public class OrderValidationResponse
{
    public Order Order { get; set; } = new();
    public ValidationResult ValidationResult { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}

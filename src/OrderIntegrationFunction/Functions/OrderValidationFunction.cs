using BidOne.OrderIntegrationFunction.Services;
using BidOne.Shared.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BidOne.OrderIntegrationFunction.Functions;

public class OrderValidationFunction
{
    private readonly IOrderValidationService _validationService;
    private readonly ILogger<OrderValidationFunction> _logger;

    public OrderValidationFunction(
        IOrderValidationService validationService,
        ILogger<OrderValidationFunction> logger)
    {
        _validationService = validationService;
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
}

public class OrderValidationResponse
{
    public Order Order { get; set; } = new();
    public ValidationResult ValidationResult { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}
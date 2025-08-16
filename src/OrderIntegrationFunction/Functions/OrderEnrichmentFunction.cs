using BidOne.OrderIntegrationFunction.Services;
using BidOne.Shared.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BidOne.OrderIntegrationFunction.Functions;

public class OrderEnrichmentFunction
{
    private readonly IOrderEnrichmentService _enrichmentService;
    private readonly ILogger<OrderEnrichmentFunction> _logger;

    public OrderEnrichmentFunction(
        IOrderEnrichmentService enrichmentService,
        ILogger<OrderEnrichmentFunction> logger)
    {
        _enrichmentService = enrichmentService;
        _logger = logger;
    }

    [Function("EnrichOrderData")]
    public async Task<EnrichmentResult> EnrichOrderData(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] Microsoft.Azure.Functions.Worker.Http.HttpRequestData req)
    {
        _logger.LogInformation("Order enrichment function triggered via HTTP");

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
                _logger.LogWarning("Invalid order data received for enrichment");
                return new EnrichmentResult
                {
                    IsSuccessful = false,
                    EnrichedOrder = new Order(),
                    Warnings = new List<string> { "Invalid order data format" },
                    EnrichedAt = DateTime.UtcNow
                };
            }

            _logger.LogInformation("Enriching order {OrderId} for customer {CustomerId}",
                order.Id, order.CustomerId);

            var result = await _enrichmentService.EnrichOrderAsync(order);

            _logger.LogInformation("Order {OrderId} enrichment completed. IsSuccessful: {IsSuccessful}, WarningCount: {WarningCount}",
                order.Id, result.IsSuccessful, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during order enrichment");

            return new EnrichmentResult
            {
                IsSuccessful = false,
                EnrichedOrder = new Order(),
                Warnings = new List<string> { "An error occurred during order enrichment" },
                EnrichedAt = DateTime.UtcNow
            };
        }
    }

    [Function("EnrichOrderFromServiceBus")]
    [ServiceBusOutput("order-enriched", Connection = "ServiceBusConnection")]
    public async Task<string?> EnrichOrderFromServiceBus(
        [ServiceBusTrigger("order-validated", Connection = "ServiceBusConnection")] string validatedOrderMessage)
    {
        _logger.LogInformation("Order enrichment function triggered from Service Bus");

        try
        {
            var validationResponse = JsonSerializer.Deserialize<OrderValidationResponse>(validatedOrderMessage, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (validationResponse?.Order == null)
            {
                _logger.LogError("Failed to deserialize validation response from Service Bus message");
                throw new InvalidOperationException("Invalid validation response data");
            }

            // Only process valid orders
            if (!validationResponse.ValidationResult.IsValid)
            {
                _logger.LogWarning("Skipping enrichment for invalid order {OrderId}", validationResponse.Order.Id);
                
                // Send to failed queue instead
                var failedResponse = new OrderEnrichmentResponse
                {
                    Order = validationResponse.Order,
                    ValidationResult = validationResponse.ValidationResult,
                    EnrichmentResult = new EnrichmentResult
                    {
                        IsSuccessful = false,
                        EnrichedOrder = validationResponse.Order,
                        Warnings = new List<string> { "Order validation failed" },
                        EnrichedAt = DateTime.UtcNow
                    },
                    ProcessedAt = DateTime.UtcNow
                };

                var failedJson = JsonSerializer.Serialize(failedResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                // Send to failure queue (would need another output binding)
                _logger.LogWarning("Order {OrderId} marked as failed due to validation errors", validationResponse.Order.Id);
                return null;
            }

            _logger.LogInformation("Enriching validated order {OrderId}", validationResponse.Order.Id);

            var enrichmentResult = await _enrichmentService.EnrichOrderAsync(validationResponse.Order);

            // Create enrichment response
            var response = new OrderEnrichmentResponse
            {
                Order = validationResponse.Order,
                ValidationResult = validationResponse.ValidationResult,
                EnrichmentResult = enrichmentResult,
                ProcessedAt = DateTime.UtcNow
            };

            var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Order {OrderId} enrichment result sent to order-enriched queue", validationResponse.Order.Id);
            return responseJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order enrichment from Service Bus");
            throw; // This will move the message to dead letter queue
        }
    }

    [Function("ProcessEnrichedOrder")]
    [ServiceBusOutput("order-processing", Connection = "ServiceBusConnection")]
    public async Task<string?> ProcessEnrichedOrder(
        [ServiceBusTrigger("order-enriched", Connection = "ServiceBusConnection")] string enrichedOrderMessage)
    {
        _logger.LogInformation("Processing enriched order from Service Bus");

        try
        {
            var enrichmentResponse = JsonSerializer.Deserialize<OrderEnrichmentResponse>(enrichedOrderMessage, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (enrichmentResponse?.EnrichmentResult == null)
            {
                _logger.LogError("Failed to deserialize enrichment response from Service Bus message");
                return null;
            }

            if (!enrichmentResponse.EnrichmentResult.IsSuccessful)
            {
                _logger.LogWarning("Skipping processing for unsuccessful enrichment of order {OrderId}", 
                    enrichmentResponse.Order.Id);
                return null;
            }

            _logger.LogInformation("Preparing enriched order {OrderId} for processing", enrichmentResponse.Order.Id);

            // Create processing request
            var processingRequest = new ProcessOrderRequest
            {
                Order = enrichmentResponse.EnrichmentResult.EnrichedOrder,
                EnrichmentData = enrichmentResponse.EnrichmentResult.EnrichmentData,
                ProcessedAt = DateTime.UtcNow
            };

            var requestJson = JsonSerializer.Serialize(processingRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogInformation("Order {OrderId} sent to order-processing queue", enrichmentResponse.Order.Id);
            return requestJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing enriched order from Service Bus");
            throw; // This will move the message to dead letter queue
        }
    }
}

public class OrderEnrichmentResponse
{
    public Order Order { get; set; } = new();
    public ValidationResult ValidationResult { get; set; } = new();
    public EnrichmentResult EnrichmentResult { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}
using BidOne.Shared.Models;
using Microsoft.Extensions.Logging;

namespace BidOne.OrderIntegrationFunction.Services;

public class OrderEnrichmentService : IOrderEnrichmentService
{
    private readonly ProductEnrichmentDbContext _dbContext;
    private readonly IExternalDataService _externalDataService;
    private readonly ILogger<OrderEnrichmentService> _logger;

    public OrderEnrichmentService(
        ProductEnrichmentDbContext dbContext,
        IExternalDataService externalDataService,
        ILogger<OrderEnrichmentService> logger)
    {
        _dbContext = dbContext;
        _externalDataService = externalDataService;
        _logger = logger;
    }

    public async Task<EnrichmentResult> EnrichOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        var result = new EnrichmentResult
        {
            IsSuccessful = true,
            EnrichedOrder = CloneOrder(order),
            Warnings = new List<string>(),
            EnrichedAt = DateTime.UtcNow,
            EnrichmentData = new Dictionary<string, object>()
        };

        try
        {
            _logger.LogInformation("Starting enrichment for order {OrderId}", order.Id);

            // Enrich customer data
            await EnrichCustomerData(result, cancellationToken);

            // Enrich product data for each item
            await EnrichProductData(result, cancellationToken);

            // Enrich delivery information
            await EnrichDeliveryData(result, cancellationToken);

            // Calculate totals and additional data
            CalculateEnrichedTotals(result);

            // Add metadata
            result.EnrichmentData["EnrichmentDuration"] = DateTime.UtcNow.Subtract(result.EnrichedAt).TotalMilliseconds;
            result.EnrichmentData["EnrichedFields"] = GetEnrichedFieldsList(result);

            _logger.LogInformation("Enrichment completed for order {OrderId}. Warnings: {WarningCount}",
                order.Id, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during order enrichment for order {OrderId}", order.Id);

            result.IsSuccessful = false;
            result.Warnings.Add($"Enrichment failed: {ex.Message}");

            return result;
        }
    }

    private async Task EnrichCustomerData(EnrichmentResult result, CancellationToken cancellationToken)
    {
        try
        {
            var customerData = await _externalDataService.GetCustomerDataAsync(
                result.EnrichedOrder.CustomerId, cancellationToken);

            if (customerData != null)
            {
                result.EnrichedOrder.CustomerEmail = customerData.Email;
                result.EnrichedOrder.CustomerPhone = customerData.Phone;

                result.EnrichmentData["CustomerName"] = customerData.Name;
                result.EnrichmentData["CustomerTier"] = customerData.CustomerTier;
                result.EnrichmentData["CreditLimit"] = customerData.CreditLimit;
                result.EnrichmentData["CurrentBalance"] = customerData.CurrentBalance;
                result.EnrichmentData["PreferredDeliveryMethod"] = customerData.PreferredDeliveryMethod;
                result.EnrichmentData["PreferredProducts"] = customerData.PreferredProducts;

                // Check credit limit
                var orderTotal = result.EnrichedOrder.Items.Sum(i => i.TotalPrice);
                if (customerData.CurrentBalance + orderTotal > customerData.CreditLimit)
                {
                    result.Warnings.Add($"Order total ${orderTotal:N2} would exceed customer credit limit");
                }

                _logger.LogDebug("Customer data enriched for customer {CustomerId}", result.EnrichedOrder.CustomerId);
            }
            else
            {
                result.Warnings.Add($"Customer data not found for customer {result.EnrichedOrder.CustomerId}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich customer data for {CustomerId}", result.EnrichedOrder.CustomerId);
            result.Warnings.Add($"Customer data enrichment failed: {ex.Message}");
        }
    }

    private async Task EnrichProductData(EnrichmentResult result, CancellationToken cancellationToken)
    {
        var productEnrichmentTasks = result.EnrichedOrder.Items.Select(async (item, index) =>
        {
            try
            {
                var productData = await _externalDataService.GetProductDataAsync(item.ProductId, cancellationToken);

                if (productData != null)
                {
                    // Enrich product information
                    item.ProductName = productData.Name;
                    item.Category = productData.Category;

                    // Add enrichment data
                    result.EnrichmentData[$"Product_{item.ProductId}_Description"] = productData.Description;
                    result.EnrichmentData[$"Product_{item.ProductId}_Weight"] = productData.Weight;
                    result.EnrichmentData[$"Product_{item.ProductId}_WeightUnit"] = productData.WeightUnit;
                    result.EnrichmentData[$"Product_{item.ProductId}_Supplier"] = productData.Supplier;
                    result.EnrichmentData[$"Product_{item.ProductId}_LeadTimeDays"] = productData.LeadTimeDays;
                    result.EnrichmentData[$"Product_{item.ProductId}_Allergens"] = productData.Allergens;
                    result.EnrichmentData[$"Product_{item.ProductId}_NutritionalInfo"] = productData.NutritionalInfo;

                    // Check lead time warnings
                    if (result.EnrichedOrder.DeliveryDate.HasValue)
                    {
                        var requestedDeliveryDays = (result.EnrichedOrder.DeliveryDate.Value - DateTime.UtcNow).Days;
                        if (requestedDeliveryDays < productData.LeadTimeDays)
                        {
                            result.Warnings.Add($"Product {item.ProductName} requires {productData.LeadTimeDays} days lead time, but delivery is requested in {requestedDeliveryDays} days");
                        }
                    }

                    _logger.LogDebug("Product data enriched for product {ProductId}", item.ProductId);
                }
                else
                {
                    result.Warnings.Add($"Product data not found for product {item.ProductId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to enrich product data for {ProductId}", item.ProductId);
                result.Warnings.Add($"Product data enrichment failed for {item.ProductId}: {ex.Message}");
            }
        });

        await Task.WhenAll(productEnrichmentTasks);
    }

    private async Task EnrichDeliveryData(EnrichmentResult result, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(result.EnrichedOrder.DeliveryAddress) &&
                result.EnrichedOrder.DeliveryDate.HasValue)
            {
                var deliveryData = await _externalDataService.GetDeliveryDataAsync(
                    result.EnrichedOrder.DeliveryAddress,
                    result.EnrichedOrder.DeliveryDate.Value,
                    cancellationToken);

                if (deliveryData != null)
                {
                    result.EnrichmentData["DeliveryMethod"] = deliveryData.DeliveryMethod;
                    result.EnrichmentData["EstimatedDeliveryCost"] = deliveryData.EstimatedCost;
                    result.EnrichmentData["EstimatedDeliveryDays"] = deliveryData.EstimatedDays;
                    result.EnrichmentData["CarrierName"] = deliveryData.CarrierName;
                    result.EnrichmentData["IsExpressAvailable"] = deliveryData.IsExpressAvailable;
                    result.EnrichmentData["ExpressCost"] = deliveryData.ExpressCost;
                    result.EnrichmentData["AvailableDeliveryDates"] = deliveryData.AvailableDeliveryDates;

                    // Validate delivery address
                    var isValidAddress = await _externalDataService.ValidateAddressAsync(
                        result.EnrichedOrder.DeliveryAddress, cancellationToken);

                    if (!isValidAddress)
                    {
                        result.Warnings.Add("Delivery address could not be validated");
                    }

                    _logger.LogDebug("Delivery data enriched for address {Address}", result.EnrichedOrder.DeliveryAddress);
                }
                else
                {
                    result.Warnings.Add("Delivery data could not be retrieved");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich delivery data");
            result.Warnings.Add($"Delivery data enrichment failed: {ex.Message}");
        }
    }

    private static void CalculateEnrichedTotals(EnrichmentResult result)
    {
        var order = result.EnrichedOrder;

        // Calculate totals
        var subtotal = order.Items.Sum(i => i.TotalPrice);
        var totalWeight = 0m;
        var categoryCount = order.Items.Select(i => i.Category).Distinct().Count();

        // Calculate total weight if available
        foreach (var item in order.Items)
        {
            if (result.EnrichmentData.TryGetValue($"Product_{item.ProductId}_Weight", out var weightObj) &&
                weightObj is decimal weight)
            {
                totalWeight += weight * item.Quantity;
            }
        }

        result.EnrichmentData["Subtotal"] = subtotal;
        result.EnrichmentData["TotalWeight"] = totalWeight;
        result.EnrichmentData["CategoryCount"] = categoryCount;
        result.EnrichmentData["ItemCount"] = order.Items.Count;
        result.EnrichmentData["AverageItemPrice"] = order.Items.Count > 0 ? order.Items.Average(i => i.UnitPrice) : 0;

        // Calculate estimated delivery cost if available
        if (result.EnrichmentData.TryGetValue("EstimatedDeliveryCost", out var deliveryCostObj) &&
            deliveryCostObj is decimal deliveryCost)
        {
            result.EnrichmentData["EstimatedTotal"] = subtotal + deliveryCost;
        }
    }

    private static List<string> GetEnrichedFieldsList(EnrichmentResult result)
    {
        var enrichedFields = new List<string>();

        if (!string.IsNullOrEmpty(result.EnrichedOrder.CustomerEmail))
            enrichedFields.Add("CustomerEmail");

        if (!string.IsNullOrEmpty(result.EnrichedOrder.CustomerPhone))
            enrichedFields.Add("CustomerPhone");

        foreach (var item in result.EnrichedOrder.Items)
        {
            if (!string.IsNullOrEmpty(item.ProductName))
                enrichedFields.Add($"Product_{item.ProductId}_Name");

            if (!string.IsNullOrEmpty(item.Category))
                enrichedFields.Add($"Product_{item.ProductId}_Category");
        }

        if (result.EnrichmentData.ContainsKey("DeliveryMethod"))
            enrichedFields.Add("DeliveryData");

        return enrichedFields;
    }

    private static Order CloneOrder(Order original)
    {
        return new Order
        {
            Id = original.Id,
            CustomerId = original.CustomerId,
            CustomerEmail = original.CustomerEmail,
            CustomerPhone = original.CustomerPhone,
            Items = original.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Category = item.Category
            }).ToList(),
            Status = original.Status,
            CreatedAt = original.CreatedAt,
            DeliveryDate = original.DeliveryDate,
            DeliveryAddress = original.DeliveryAddress,
            SpecialInstructions = original.SpecialInstructions
        };
    }
}

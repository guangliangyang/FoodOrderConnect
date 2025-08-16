using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BidOne.OrderIntegrationFunction.Services;

public class ExternalDataService : IExternalDataService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExternalDataService> _logger;

    public ExternalDataService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ExternalDataService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CustomerData?> GetCustomerDataAsync(string customerId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching customer data for customer {CustomerId}", customerId);

            var baseUrl = _configuration["ExternalServices:CustomerApiUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogWarning("Customer API URL not configured");
                return await GetMockCustomerData(customerId);
            }

            var response = await _httpClient.GetAsync($"{baseUrl}/customers/{customerId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var customerData = JsonSerializer.Deserialize<CustomerData>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogDebug("Customer data retrieved for customer {CustomerId}", customerId);
                return customerData;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Customer {CustomerId} not found in external system", customerId);
                return null;
            }

            _logger.LogWarning("Failed to get customer data for {CustomerId}. Status: {StatusCode}",
                customerId, response.StatusCode);

            // Fallback to mock data for demo purposes
            return await GetMockCustomerData(customerId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching customer data for {CustomerId}", customerId);
            return await GetMockCustomerData(customerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching customer data for {CustomerId}", customerId);
            return null;
        }
    }

    public async Task<ProductData?> GetProductDataAsync(string productId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching product data for product {ProductId}", productId);

            var baseUrl = _configuration["ExternalServices:ProductApiUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogWarning("Product API URL not configured");
                return await GetMockProductData(productId);
            }

            var response = await _httpClient.GetAsync($"{baseUrl}/products/{productId}", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var productData = JsonSerializer.Deserialize<ProductData>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogDebug("Product data retrieved for product {ProductId}", productId);
                return productData;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Product {ProductId} not found in external system", productId);
                return null;
            }

            _logger.LogWarning("Failed to get product data for {ProductId}. Status: {StatusCode}",
                productId, response.StatusCode);

            // Fallback to mock data for demo purposes
            return await GetMockProductData(productId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching product data for {ProductId}", productId);
            return await GetMockProductData(productId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product data for {ProductId}", productId);
            return null;
        }
    }

    public async Task<DeliveryData?> GetDeliveryDataAsync(string deliveryAddress, DateTime deliveryDate, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Fetching delivery data for address {Address} on {Date}", deliveryAddress, deliveryDate);

            var baseUrl = _configuration["ExternalServices:DeliveryApiUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogWarning("Delivery API URL not configured");
                return await GetMockDeliveryData(deliveryAddress, deliveryDate);
            }

            var requestData = new
            {
                Address = deliveryAddress,
                RequestedDate = deliveryDate.ToString("yyyy-MM-dd")
            };

            var jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{baseUrl}/delivery/quote", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var deliveryData = JsonSerializer.Deserialize<DeliveryData>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogDebug("Delivery data retrieved for address {Address}", deliveryAddress);
                return deliveryData;
            }

            _logger.LogWarning("Failed to get delivery data for {Address}. Status: {StatusCode}",
                deliveryAddress, response.StatusCode);

            // Fallback to mock data for demo purposes
            return await GetMockDeliveryData(deliveryAddress, deliveryDate);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching delivery data for {Address}", deliveryAddress);
            return await GetMockDeliveryData(deliveryAddress, deliveryDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching delivery data for {Address}", deliveryAddress);
            return null;
        }
    }

    public async Task<bool> ValidateAddressAsync(string address, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Validating address {Address}", address);

            var baseUrl = _configuration["ExternalServices:AddressValidationApiUrl"];
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogWarning("Address validation API URL not configured");
                return await MockValidateAddress(address);
            }

            var requestData = new { Address = address };
            var jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{baseUrl}/validate", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<AddressValidationResult>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogDebug("Address validation completed for {Address}. IsValid: {IsValid}", address, result?.IsValid ?? false);
                return result?.IsValid ?? false;
            }

            _logger.LogWarning("Failed to validate address {Address}. Status: {StatusCode}", address, response.StatusCode);
            return await MockValidateAddress(address);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while validating address {Address}", address);
            return await MockValidateAddress(address);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating address {Address}", address);
            return false;
        }
    }

    // Mock data methods for demo purposes
    private async Task<CustomerData> GetMockCustomerData(string customerId)
    {
        await Task.Delay(100); // Simulate API delay

        return new CustomerData
        {
            Id = customerId,
            Name = $"Customer {customerId}",
            Email = $"customer{customerId}@example.com",
            Phone = "+1-555-0123",
            PreferredDeliveryMethod = "Standard",
            CreditLimit = 50000m,
            CurrentBalance = 1500m,
            CustomerTier = "Gold",
            PreferredProducts = new List<string> { "PROD001", "PROD002" },
            Metadata = new Dictionary<string, object>
            {
                ["JoinDate"] = DateTime.UtcNow.AddYears(-2),
                ["LastOrderDate"] = DateTime.UtcNow.AddDays(-30),
                ["TotalOrders"] = 45
            }
        };
    }

    private async Task<ProductData> GetMockProductData(string productId)
    {
        await Task.Delay(100); // Simulate API delay

        var categories = new[] { "Electronics", "Clothing", "Books", "Home & Garden", "Sports" };
        var suppliers = new[] { "Supplier A", "Supplier B", "Supplier C" };
        var allergens = new[] { "Nuts", "Dairy", "Gluten", "Soy" };

        var random = new Random(productId.GetHashCode());

        return new ProductData
        {
            Id = productId,
            Name = $"Product {productId}",
            Description = $"High-quality product {productId} for all your needs",
            Category = categories[random.Next(categories.Length)],
            Weight = Math.Round((decimal)(random.NextDouble() * 10 + 0.1), 2),
            WeightUnit = "kg",
            Supplier = suppliers[random.Next(suppliers.Length)],
            LeadTimeDays = random.Next(1, 14),
            Allergens = allergens.Take(random.Next(0, 3)).ToList(),
            NutritionalInfo = new Dictionary<string, object>
            {
                ["Calories"] = random.Next(50, 500),
                ["Protein"] = Math.Round(random.NextDouble() * 20, 1),
                ["Carbs"] = Math.Round(random.NextDouble() * 50, 1),
                ["Fat"] = Math.Round(random.NextDouble() * 30, 1)
            },
            Metadata = new Dictionary<string, object>
            {
                ["ManufactureDate"] = DateTime.UtcNow.AddDays(-random.Next(1, 90)),
                ["ExpiryDate"] = DateTime.UtcNow.AddDays(random.Next(30, 365)),
                ["BatchNumber"] = $"BATCH{random.Next(1000, 9999)}"
            }
        };
    }

    private async Task<DeliveryData> GetMockDeliveryData(string deliveryAddress, DateTime deliveryDate)
    {
        await Task.Delay(150); // Simulate API delay

        var methods = new[] { "Standard", "Express", "Overnight" };
        var carriers = new[] { "FedEx", "UPS", "DHL", "USPS" };
        
        var random = new Random(deliveryAddress.GetHashCode());
        var baseDate = DateTime.UtcNow.Date.AddDays(1);

        return new DeliveryData
        {
            DeliveryMethod = methods[random.Next(methods.Length)],
            EstimatedCost = Math.Round((decimal)(random.NextDouble() * 50 + 5), 2),
            EstimatedDays = random.Next(1, 7),
            CarrierName = carriers[random.Next(carriers.Length)],
            TrackingNumber = $"TRK{random.Next(100000000, 999999999)}",
            IsExpressAvailable = random.NextDouble() > 0.3,
            ExpressCost = Math.Round((decimal)(random.NextDouble() * 30 + 15), 2),
            AvailableDeliveryDates = Enumerable.Range(0, 14)
                .Select(i => baseDate.AddDays(i))
                .Where(d => d.DayOfWeek != DayOfWeek.Sunday)
                .ToList(),
            Metadata = new Dictionary<string, object>
            {
                ["Zone"] = $"Zone {random.Next(1, 6)}",
                ["ServiceLevel"] = "Premium",
                ["InsuranceIncluded"] = random.NextDouble() > 0.5
            }
        };
    }

    private async Task<bool> MockValidateAddress(string address)
    {
        await Task.Delay(200); // Simulate API delay

        // Simple mock validation - reject obviously invalid addresses
        if (string.IsNullOrWhiteSpace(address) || address.Length < 10)
            return false;

        // Mock some addresses as invalid for testing
        var invalidPatterns = new[] { "invalid", "bad", "error", "fail" };
        return !invalidPatterns.Any(pattern => address.ToLower().Contains(pattern));
    }
}

public class AddressValidationResult
{
    public bool IsValid { get; set; }
    public string? CorrectedAddress { get; set; }
    public List<string> Warnings { get; set; } = new();
}
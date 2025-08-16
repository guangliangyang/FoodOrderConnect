namespace BidOne.OrderIntegrationFunction.Services;

public interface IExternalDataService
{
    Task<CustomerData?> GetCustomerDataAsync(string customerId, CancellationToken cancellationToken = default);
    Task<ProductData?> GetProductDataAsync(string productId, CancellationToken cancellationToken = default);
    Task<DeliveryData?> GetDeliveryDataAsync(string deliveryAddress, DateTime deliveryDate, CancellationToken cancellationToken = default);
    Task<bool> ValidateAddressAsync(string address, CancellationToken cancellationToken = default);
}

public class CustomerData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PreferredDeliveryMethod { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public decimal CurrentBalance { get; set; }
    public string CustomerTier { get; set; } = string.Empty;
    public List<string> PreferredProducts { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ProductData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public string WeightUnit { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public int LeadTimeDays { get; set; }
    public List<string> Allergens { get; set; } = new();
    public Dictionary<string, object> NutritionalInfo { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class DeliveryData
{
    public string DeliveryMethod { get; set; } = string.Empty;
    public decimal EstimatedCost { get; set; }
    public int EstimatedDays { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public bool IsExpressAvailable { get; set; }
    public decimal ExpressCost { get; set; }
    public List<DateTime> AvailableDeliveryDates { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

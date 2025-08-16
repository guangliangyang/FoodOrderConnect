using System.ComponentModel.DataAnnotations;

namespace BidOne.Shared.Models;

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? SpecialInstructions { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice => Quantity * UnitPrice;
    public string? Category { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum OrderStatus
{
    Received = 0,
    Validating = 1,
    Validated = 2,
    Enriching = 3,
    Enriched = 4,
    Processing = 5,
    Confirmed = 6,
    Failed = 7,
    Cancelled = 8,
    Delivered = 9
}

public class CreateOrderRequest
{
    [Required]
    public string CustomerId { get; set; } = string.Empty;

    [Required]
    public List<CreateOrderItemRequest> Items { get; set; } = new();

    public DateTime? DeliveryDate { get; set; }
    public string? Notes { get; set; }
}

public class CreateOrderItemRequest
{
    [Required]
    public string ProductId { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}

public class OrderResponse
{
    public string OrderId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ProcessOrderRequest
{
    public Order Order { get; set; } = new();
    public Dictionary<string, object> EnrichmentData { get; set; } = new();
    public DateTime ProcessedAt { get; set; }
}

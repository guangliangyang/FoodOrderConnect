using System.ComponentModel.DataAnnotations;
using BidOne.Shared.Domain;
using BidOne.Shared.Domain.Events;
using BidOne.Shared.Domain.ValueObjects;

namespace BidOne.Shared.Models;

public class Order : AggregateRoot
{
    public OrderId Id { get; set; }
    public CustomerId CustomerId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string SupplierId { get; private set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public OrderStatus Status { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? SpecialInstructions { get; set; }
    public Money TotalAmount { get; private set; } = Money.Zero();
    public string? Notes { get; private set; }
    public Dictionary<string, object> Metadata { get; private set; } = new();

    public Order()
    {
        Id = OrderId.CreateNew();
        CustomerId = CustomerId.Create("UNKNOWN");
    }

    private Order(OrderId id, CustomerId customerId)
    {
        Id = id;
        CustomerId = customerId;
        Status = OrderStatus.Received;
        AddDomainEvent(new OrderCreatedEvent(Id, CustomerId));
    }

    public static Order Create(OrderId id, CustomerId customerId)
    {
        return new Order(id, customerId);
    }

    public static Order Create(CustomerId customerId)
    {
        return new Order(OrderId.CreateNew(), customerId);
    }

    public void AddItem(ProductInfo productInfo, Quantity quantity, Money unitPrice)
    {
        if (Status != OrderStatus.Received)
            throw new InvalidOperationException($"Cannot add items to order in status {Status}");

        var orderItem = OrderItem.Create(productInfo, quantity, unitPrice);
        Items.Add(orderItem);
        RecalculateTotalAmount();
        UpdateTimestamp();
    }

    public void RemoveItem(string productId)
    {
        if (Status != OrderStatus.Received)
            throw new InvalidOperationException($"Cannot remove items from order in status {Status}");

        var item = Items.FirstOrDefault(i => i.ProductInfo.ProductId == productId);
        if (item != null)
        {
            Items.Remove(item);
            RecalculateTotalAmount();
            UpdateTimestamp();
        }
    }

    public void UpdateDeliveryInfo(DateTime? deliveryDate, string? deliveryAddress)
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot update delivery info for order in status {Status}");

        DeliveryDate = deliveryDate;
        DeliveryAddress = deliveryAddress;
        UpdateTimestamp();
    }

    public void SetSpecialInstructions(string? instructions)
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot update instructions for order in status {Status}");

        SpecialInstructions = instructions;
        UpdateTimestamp();
    }

    public void SetNotes(string? notes)
    {
        Notes = notes;
        UpdateTimestamp();
    }

    public void Validate()
    {
        if (Status != OrderStatus.Received)
            throw new InvalidOperationException($"Cannot validate order in status {Status}");

        if (!Items.Any())
            throw new InvalidOperationException("Cannot validate order without items");

        Status = OrderStatus.Validating;
        UpdateTimestamp();
        AddDomainEvent(new OrderValidationStartedEvent(Id));
    }

    public void MarkAsValidated()
    {
        if (Status != OrderStatus.Validating)
            throw new InvalidOperationException($"Cannot mark order as validated from status {Status}");

        Status = OrderStatus.Validated;
        UpdateTimestamp();
        AddDomainEvent(new OrderValidatedEvent(Id));
    }

    public void StartEnrichment()
    {
        if (Status != OrderStatus.Validated)
            throw new InvalidOperationException($"Cannot start enrichment from status {Status}");

        Status = OrderStatus.Enriching;
        UpdateTimestamp();
    }

    public void CompleteEnrichment(Dictionary<string, object> enrichmentData)
    {
        if (Status != OrderStatus.Enriching)
            throw new InvalidOperationException($"Cannot complete enrichment from status {Status}");

        foreach (var kvp in enrichmentData)
        {
            Metadata[kvp.Key] = kvp.Value;
        }

        Status = OrderStatus.Enriched;
        UpdateTimestamp();
        AddDomainEvent(new OrderEnrichedEvent(Id, enrichmentData));
    }

    public void StartProcessing()
    {
        if (Status != OrderStatus.Enriched)
            throw new InvalidOperationException($"Cannot start processing from status {Status}");

        Status = OrderStatus.Processing;
        UpdateTimestamp();
    }

    public void Confirm(string supplierId)
    {
        if (Status != OrderStatus.Processing)
            throw new InvalidOperationException($"Cannot confirm order from status {Status}");

        SupplierId = supplierId;
        Status = OrderStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        UpdateTimestamp();
        AddDomainEvent(new OrderConfirmedEvent(Id, SupplierId, TotalAmount));
    }

    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Delivered or OrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel order in status {Status}");

        Status = OrderStatus.Cancelled;
        Metadata["CancellationReason"] = reason;
        Metadata["CancelledAt"] = DateTime.UtcNow;
        UpdateTimestamp();
        AddDomainEvent(new OrderCancelledEvent(Id, reason));
    }

    public void MarkAsFailed(string reason)
    {
        Status = OrderStatus.Failed;
        Metadata["FailureReason"] = reason;
        Metadata["FailedAt"] = DateTime.UtcNow;
        UpdateTimestamp();
        AddDomainEvent(new OrderFailedEvent(Id, reason));
    }

    public void MarkAsDelivered()
    {
        if (Status != OrderStatus.Confirmed)
            throw new InvalidOperationException($"Cannot mark order as delivered from status {Status}");

        Status = OrderStatus.Delivered;
        Metadata["DeliveredAt"] = DateTime.UtcNow;
        UpdateTimestamp();
        AddDomainEvent(new OrderDeliveredEvent(Id));
    }

    public bool CanBeCancelled()
    {
        return Status is OrderStatus.Received or OrderStatus.Validating or OrderStatus.Validated;
    }

    public bool IsHighValue(decimal threshold = 1000m)
    {
        return TotalAmount.Amount > threshold;
    }

    private void RecalculateTotalAmount()
    {
        TotalAmount = Items.Aggregate(Money.Zero(), (total, item) => total.Add(item.GetTotalPrice()));
    }

    public void SetCustomerContact(string email, string phone)
    {
        if (!string.IsNullOrWhiteSpace(email))
            CustomerEmail = email;
        if (!string.IsNullOrWhiteSpace(phone))
            CustomerPhone = phone;
        UpdateTimestamp();
    }
}

public class OrderItem : Entity
{
    public ProductInfo ProductInfo { get; set; }
    public Quantity Quantity { get; set; }
    public Money UnitPrice { get; set; }
    public Dictionary<string, object> Properties { get; private set; } = new();

    public OrderItem()
    {
        ProductInfo = ProductInfo.Create("UNKNOWN", "UNKNOWN");
        Quantity = Quantity.Create(1);
        UnitPrice = Money.Zero();
    }

    private OrderItem(ProductInfo productInfo, Quantity quantity, Money unitPrice)
    {
        ProductInfo = productInfo ?? throw new ArgumentNullException(nameof(productInfo));
        Quantity = quantity ?? throw new ArgumentNullException(nameof(quantity));
        UnitPrice = unitPrice ?? throw new ArgumentNullException(nameof(unitPrice));
    }

    public static OrderItem Create(ProductInfo productInfo, Quantity quantity, Money unitPrice)
    {
        return new OrderItem(productInfo, quantity, unitPrice);
    }

    public Money GetTotalPrice()
    {
        return UnitPrice.Multiply(Quantity.Value);
    }

    public void UpdateQuantity(Quantity newQuantity)
    {
        Quantity = newQuantity ?? throw new ArgumentNullException(nameof(newQuantity));
        UpdateTimestamp();
    }

    public void UpdateUnitPrice(Money newUnitPrice)
    {
        UnitPrice = newUnitPrice ?? throw new ArgumentNullException(nameof(newUnitPrice));
        UpdateTimestamp();
    }

    public void SetProperty(string key, object value)
    {
        Properties[key] = value;
        UpdateTimestamp();
    }

    // Backward compatibility properties
    public string ProductId
    {
        get => ProductInfo.ProductId;
        set => ProductInfo = ProductInfo.Create(value, ProductInfo.ProductName, ProductInfo.Category);
    }
    public string ProductName
    {
        get => ProductInfo.ProductName;
        set => ProductInfo = ProductInfo.Create(ProductInfo.ProductId, value, ProductInfo.Category);
    }
    public string? Category
    {
        get => ProductInfo.Category;
        set => ProductInfo = ProductInfo.Create(ProductInfo.ProductId, ProductInfo.ProductName, value);
    }
    public decimal TotalPrice => GetTotalPrice().Amount;
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

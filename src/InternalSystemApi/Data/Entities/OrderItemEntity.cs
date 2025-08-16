namespace BidOne.InternalSystemApi.Data.Entities;

public class OrderItemEntity : IEntity<Guid>
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? Category { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public OrderEntity Order { get; set; } = null!;
    public ProductEntity Product { get; set; } = null!;

    object? IEntity.Id => Id;
}

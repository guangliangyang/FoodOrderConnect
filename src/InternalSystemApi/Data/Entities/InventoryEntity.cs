namespace BidOne.InternalSystemApi.Data.Entities;

public class InventoryEntity : IEntity<Guid>
{
    public Guid Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int ReorderLevel { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public ProductEntity Product { get; set; } = null!;

    // Computed properties
    public int AvailableQuantity => QuantityOnHand - QuantityReserved;
    public bool IsLowStock => AvailableQuantity <= ReorderLevel;

    object? IEntity.Id => Id;
}

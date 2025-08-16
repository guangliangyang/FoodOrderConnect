namespace BidOne.InternalSystemApi.Data.Entities;

public class ProductEntity : IEntity<string>
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? SupplierId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public SupplierEntity? Supplier { get; set; }
    public InventoryEntity? Inventory { get; set; }

    object? IEntity.Id => Id;
}
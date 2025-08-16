namespace BidOne.InternalSystemApi.Data.Entities;

public class SupplierEntity : IEntity<string>
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? ApiEndpoint { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public List<OrderEntity> Orders { get; set; } = new();
    public List<ProductEntity> Products { get; set; } = new();

    object? IEntity.Id => Id;
}

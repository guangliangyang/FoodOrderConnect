namespace BidOne.InternalSystemApi.Data.Entities;

public class CustomerEntity : IEntity<string>
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public List<OrderEntity> Orders { get; set; } = new();

    object? IEntity.Id => Id;
}

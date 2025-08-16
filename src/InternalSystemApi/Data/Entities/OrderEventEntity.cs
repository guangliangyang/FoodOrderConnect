namespace BidOne.InternalSystemApi.Data.Entities;

public class OrderEventEntity : IEntity<Guid>
{
    public Guid Id { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? EventData { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }

    object? IEntity.Id => Id;
}

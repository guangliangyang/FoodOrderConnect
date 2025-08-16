using System.ComponentModel.DataAnnotations;
using BidOne.Shared.Models;

namespace BidOne.InternalSystemApi.Data.Entities;

public class OrderEntity : IEntity<string>
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? SupplierId { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    // Navigation properties
    public CustomerEntity Customer { get; set; } = null!;
    public SupplierEntity? Supplier { get; set; }
    public List<OrderItemEntity> Items { get; set; } = new();

    object? IEntity.Id => Id;
}

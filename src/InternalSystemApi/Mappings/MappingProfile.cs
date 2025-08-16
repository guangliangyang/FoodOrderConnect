using AutoMapper;
using BidOne.InternalSystemApi.Data.Entities;
using BidOne.Shared.Models;

namespace BidOne.InternalSystemApi.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<OrderEntity, Order>()
            .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
            .ReverseMap()
            .ForMember(dest => dest.Customer, opt => opt.Ignore())
            .ForMember(dest => dest.Supplier, opt => opt.Ignore())
            .ForMember(dest => dest.Items, opt => opt.Ignore());

        CreateMap<OrderItemEntity, OrderItem>()
            .ReverseMap()
            .ForMember(dest => dest.Order, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore())
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
            .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

        CreateMap<CreateOrderRequest, OrderEntity>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => OrderStatus.Received))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.TotalAmount, opt => opt.Ignore())
            .ForMember(dest => dest.SupplierId, opt => opt.Ignore())
            .ForMember(dest => dest.ConfirmedAt, opt => opt.Ignore())
            .ForMember(dest => dest.Metadata, opt => opt.Ignore())
            .ForMember(dest => dest.Customer, opt => opt.Ignore())
            .ForMember(dest => dest.Supplier, opt => opt.Ignore())
            .ForMember(dest => dest.Items, opt => opt.Ignore());

        CreateMap<CreateOrderItemRequest, OrderItemEntity>()
            .ForMember(dest => dest.Id, opt => opt.Ignore())
            .ForMember(dest => dest.OrderId, opt => opt.Ignore())
            .ForMember(dest => dest.ProductName, opt => opt.Ignore())
            .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src => src.Quantity * src.UnitPrice))
            .ForMember(dest => dest.Category, opt => opt.Ignore())
            .ForMember(dest => dest.Properties, opt => opt.Ignore())
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.Order, opt => opt.Ignore())
            .ForMember(dest => dest.Product, opt => opt.Ignore());

        CreateMap<ProcessOrderRequest, OrderEntity>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Order.Id))
            .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.Order.CustomerId))
            .ForMember(dest => dest.SupplierId, opt => opt.MapFrom(src => src.Order.SupplierId))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Order.Status))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.Order.CreatedAt))
            .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
            .ForMember(dest => dest.DeliveryDate, opt => opt.MapFrom(src => src.Order.DeliveryDate))
            .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.Order.TotalAmount))
            .ForMember(dest => dest.Notes, opt => opt.MapFrom(src => src.Order.Notes))
            .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => MergeMetadata(src.Order.Metadata, src.EnrichmentData)))
            .ForMember(dest => dest.Customer, opt => opt.Ignore())
            .ForMember(dest => dest.Supplier, opt => opt.Ignore())
            .ForMember(dest => dest.Items, opt => opt.Ignore());

        CreateMap<ProductEntity, Product>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
            .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
            .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category))
            .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
            .ForMember(dest => dest.Unit, opt => opt.MapFrom(src => src.Unit))
            .ForMember(dest => dest.SupplierId, opt => opt.MapFrom(src => src.SupplierId))
            .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => src.IsActive));

        CreateMap<InventoryEntity, Inventory>()
            .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
            .ForMember(dest => dest.QuantityOnHand, opt => opt.MapFrom(src => src.QuantityOnHand))
            .ForMember(dest => dest.QuantityReserved, opt => opt.MapFrom(src => src.QuantityReserved))
            .ForMember(dest => dest.AvailableQuantity, opt => opt.MapFrom(src => src.AvailableQuantity))
            .ForMember(dest => dest.ReorderLevel, opt => opt.MapFrom(src => src.ReorderLevel))
            .ForMember(dest => dest.IsLowStock, opt => opt.MapFrom(src => src.IsLowStock))
            .ForMember(dest => dest.LastUpdated, opt => opt.MapFrom(src => src.LastUpdated));
    }

    private static Dictionary<string, object> MergeMetadata(
        Dictionary<string, object> originalMetadata,
        Dictionary<string, object> enrichmentData)
    {
        var merged = new Dictionary<string, object>(originalMetadata);
        
        foreach (var kvp in enrichmentData)
        {
            merged[kvp.Key] = kvp.Value;
        }

        merged["EnrichedAt"] = DateTime.UtcNow;
        return merged;
    }
}

// Additional DTOs for Internal System API
public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? SupplierId { get; set; }
    public bool IsActive { get; set; }
}

public class Inventory
{
    public string ProductId { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int AvailableQuantity { get; set; }
    public int ReorderLevel { get; set; }
    public bool IsLowStock { get; set; }
    public DateTime LastUpdated { get; set; }
}
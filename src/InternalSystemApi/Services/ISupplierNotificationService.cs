using BidOne.InternalSystemApi.Data.Entities;

namespace BidOne.InternalSystemApi.Services;

public interface ISupplierNotificationService
{
    Task<bool> NotifyOrderAsync(SupplierEntity supplier, OrderEntity order, CancellationToken cancellationToken = default);
    Task<bool> NotifyOrderUpdateAsync(SupplierEntity supplier, OrderEntity order, string updateType, CancellationToken cancellationToken = default);
    Task<bool> NotifyOrderCancellationAsync(SupplierEntity supplier, string orderId, string reason, CancellationToken cancellationToken = default);
}
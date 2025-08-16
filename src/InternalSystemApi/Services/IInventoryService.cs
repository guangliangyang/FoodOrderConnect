using BidOne.InternalSystemApi.Mappings;

namespace BidOne.InternalSystemApi.Services;

public interface IInventoryService
{
    Task<InventoryReservationResult> ReserveInventoryAsync(IEnumerable<InventoryReservationRequest> requests, CancellationToken cancellationToken = default);
    Task<bool> ReleaseReservationAsync(string orderId, CancellationToken cancellationToken = default);
    Task<Inventory?> GetInventoryAsync(string productId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Inventory>> GetLowStockItemsAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdateInventoryAsync(string productId, int quantityChange, string reason, CancellationToken cancellationToken = default);
}
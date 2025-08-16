using BidOne.Shared.Models;

namespace BidOne.InternalSystemApi.Services;

public interface IOrderProcessingService
{
    Task<OrderResponse> ProcessOrderAsync(ProcessOrderRequest request, CancellationToken cancellationToken = default);
    Task<OrderResponse?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);
    Task<OrderResponse> UpdateOrderStatusAsync(string orderId, OrderStatus status, string? notes = null, CancellationToken cancellationToken = default);
    Task<bool> CancelOrderAsync(string orderId, string reason, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderResponse>> GetOrdersByCustomerAsync(string customerId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderResponse>> GetOrdersBySupplierAsync(string supplierId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
}
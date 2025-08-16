using BidOne.Shared.Models;

namespace BidOne.ExternalOrderApi.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);
    Task<OrderResponse?> GetOrderStatusAsync(string orderId, CancellationToken cancellationToken = default);
    Task<OrderResponse?> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default);
}
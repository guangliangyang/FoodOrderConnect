using BidOne.Shared.Models;

namespace BidOne.OrderIntegrationFunction.Services;

/// <summary>
/// Interface for communicating with the Internal System API
/// </summary>
public interface IInternalApiClient
{
    /// <summary>
    /// Process an order by calling the Internal System API
    /// </summary>
    /// <param name="request">Order processing request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order processing response</returns>
    Task<OrderResponse> ProcessOrderAsync(ProcessOrderRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get order status from the Internal System API
    /// </summary>
    /// <param name="orderId">Order identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order response or null if not found</returns>
    Task<OrderResponse?> GetOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if the Internal System API is healthy and accessible
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the API is healthy</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
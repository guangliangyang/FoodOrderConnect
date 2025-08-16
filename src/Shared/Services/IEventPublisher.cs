using Azure.Messaging.EventGrid;

namespace BidOne.Shared.Services;

/// <summary>
/// Interface for publishing events to Azure Event Grid
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a single event to Event Grid
    /// </summary>
    /// <typeparam name="T">Event data type</typeparam>
    /// <param name="eventType">Event type (e.g., "BidOne.Order.Received")</param>
    /// <param name="subject">Event subject (e.g., "orders/12345")</param>
    /// <param name="data">Event data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishEventAsync<T>(string eventType, string subject, T data, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Publishes multiple events to Event Grid in a batch
    /// </summary>
    /// <param name="events">Collection of events to publish</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PublishEventsAsync(IEnumerable<EventGridEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an order received event
    /// </summary>
    Task PublishOrderReceivedAsync(string orderId, object orderData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an order validated event
    /// </summary>
    Task PublishOrderValidatedAsync(string orderId, bool isValid, object validationData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an order enriched event
    /// </summary>
    Task PublishOrderEnrichedAsync(string orderId, object enrichmentData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an order processed event
    /// </summary>
    Task PublishOrderProcessedAsync(string orderId, object orderData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an order failed event
    /// </summary>
    Task PublishOrderFailedAsync(string orderId, string failureReason, object orderData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes an inventory updated event
    /// </summary>
    Task PublishInventoryUpdatedAsync(string productId, int oldQuantity, int newQuantity, string changeType, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a customer profile updated event
    /// </summary>
    Task PublishCustomerProfileUpdatedAsync(string customerId, IEnumerable<string> updatedFields, string updateReason, CancellationToken cancellationToken = default);
}

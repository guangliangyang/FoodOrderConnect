using BidOne.Shared.Domain.ValueObjects;

namespace BidOne.Shared.Domain.Events;

public class OrderCreatedEvent : DomainEvent
{
    public OrderId OrderId { get; }
    public CustomerId CustomerId { get; }

    public OrderCreatedEvent(OrderId orderId, CustomerId customerId)
    {
        OrderId = orderId;
        CustomerId = customerId;
    }
}

public class OrderValidationStartedEvent : DomainEvent
{
    public OrderId OrderId { get; }

    public OrderValidationStartedEvent(OrderId orderId)
    {
        OrderId = orderId;
    }
}

public class OrderValidatedEvent : DomainEvent
{
    public OrderId OrderId { get; }

    public OrderValidatedEvent(OrderId orderId)
    {
        OrderId = orderId;
    }
}

public class OrderEnrichedEvent : DomainEvent
{
    public OrderId OrderId { get; }
    public Dictionary<string, object> EnrichmentData { get; }

    public OrderEnrichedEvent(OrderId orderId, Dictionary<string, object> enrichmentData)
    {
        OrderId = orderId;
        EnrichmentData = enrichmentData;
    }
}

public class OrderConfirmedEvent : DomainEvent
{
    public OrderId OrderId { get; }
    public string SupplierId { get; }
    public Money TotalAmount { get; }

    public OrderConfirmedEvent(OrderId orderId, string supplierId, Money totalAmount)
    {
        OrderId = orderId;
        SupplierId = supplierId;
        TotalAmount = totalAmount;
    }
}

public class OrderCancelledEvent : DomainEvent
{
    public OrderId OrderId { get; }
    public string Reason { get; }

    public OrderCancelledEvent(OrderId orderId, string reason)
    {
        OrderId = orderId;
        Reason = reason;
    }
}

public class OrderFailedEvent : DomainEvent
{
    public OrderId OrderId { get; }
    public string Reason { get; }

    public OrderFailedEvent(OrderId orderId, string reason)
    {
        OrderId = orderId;
        Reason = reason;
    }
}

public class OrderDeliveredEvent : DomainEvent
{
    public OrderId OrderId { get; }

    public OrderDeliveredEvent(OrderId orderId)
    {
        OrderId = orderId;
    }
}
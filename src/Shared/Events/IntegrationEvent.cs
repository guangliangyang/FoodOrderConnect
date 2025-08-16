using System.Text.Json;

namespace BidOne.Shared.Events;

public abstract class IntegrationEvent
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public string EventType { get; protected set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class OrderReceivedEvent : IntegrationEvent
{
    public OrderReceivedEvent()
    {
        EventType = nameof(OrderReceivedEvent);
    }

    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; }
    public string SourceSystem { get; set; } = string.Empty;
}

public class OrderValidatedEvent : IntegrationEvent
{
    public OrderValidatedEvent()
    {
        EventType = nameof(OrderValidatedEvent);
    }

    public string OrderId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public DateTime ValidatedAt { get; set; }
}

public class OrderEnrichedEvent : IntegrationEvent
{
    public OrderEnrichedEvent()
    {
        EventType = nameof(OrderEnrichedEvent);
    }

    public string OrderId { get; set; } = string.Empty;
    public Dictionary<string, object> EnrichmentData { get; set; } = new();
    public DateTime EnrichedAt { get; set; }
}

public class OrderConfirmedEvent : IntegrationEvent
{
    public OrderConfirmedEvent()
    {
        EventType = nameof(OrderConfirmedEvent);
    }

    public string OrderId { get; set; } = string.Empty;
    public string ConfirmationId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
}

public class OrderFailedEvent : IntegrationEvent
{
    public OrderFailedEvent()
    {
        EventType = nameof(OrderFailedEvent);
    }

    public string OrderId { get; set; } = string.Empty;
    public string FailureReason { get; set; } = string.Empty;
    public List<string> ErrorDetails { get; set; } = new();
    public DateTime FailedAt { get; set; }
    public bool IsRetryable { get; set; }
}

public class HighValueErrorEvent : IntegrationEvent
{
    public HighValueErrorEvent()
    {
        EventType = nameof(HighValueErrorEvent);
    }

    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string ErrorCategory { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string TechnicalDetails { get; set; } = string.Empty;
    public decimal OrderValue { get; set; }
    public string CustomerTier { get; set; } = string.Empty;
    public DateTime ErrorOccurredAt { get; set; }
    public int RetryCount { get; set; }
    public string ProcessingStage { get; set; } = string.Empty;
    public Dictionary<string, object> ContextData { get; set; } = new();
}

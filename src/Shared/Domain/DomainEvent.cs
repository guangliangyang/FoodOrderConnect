namespace BidOne.Shared.Domain;

public abstract class DomainEvent : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string EventType { get; protected set; } = string.Empty;

    protected DomainEvent()
    {
        EventType = GetType().Name;
    }
}
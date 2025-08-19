namespace BidOne.Shared.Domain;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
    string EventType { get; }
}

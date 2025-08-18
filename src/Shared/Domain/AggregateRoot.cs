using System.ComponentModel.DataAnnotations.Schema;

namespace BidOne.Shared.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void MarkEventsAsCommitted()
    {
        _domainEvents.Clear();
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
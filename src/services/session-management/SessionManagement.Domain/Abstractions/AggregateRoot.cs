using System.ComponentModel.DataAnnotations.Schema;

namespace SessionManagement.Domain.Abstractions;

/// <summary>
/// Base type for aggregate roots that record domain events. Events are collected in memory
/// while the aggregate mutates and dispatched after the transaction commits (see the
/// SavedChanges interceptor in Infrastructure). The collection is never persisted.
/// </summary>
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    [NotMapped]
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

namespace SessionManagement.Domain.Abstractions;

/// <summary>
/// A fact that has already happened inside an aggregate. Domain events carry structured
/// data only — no presentation strings; the mapping to an audit message/description lives
/// in Infrastructure. <see cref="EventId"/> is stable per raised event and doubles as the
/// idempotency key for downstream consumers.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredOnUtc { get; }
}

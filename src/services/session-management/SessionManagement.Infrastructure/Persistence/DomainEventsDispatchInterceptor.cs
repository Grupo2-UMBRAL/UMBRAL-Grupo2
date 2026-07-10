using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SessionManagement.Domain.Abstractions;
using SessionManagement.Infrastructure.Messaging;

namespace SessionManagement.Infrastructure.Persistence;

/// <summary>
/// Dispatches the domain events raised by tracked aggregates to RabbitMQ <b>after</b> the
/// transaction has committed (the <see cref="SaveChangesInterceptor.SavedChangesAsync"/> hook),
/// never inside it — publishing before commit would risk phantom events if the commit rolls
/// back (ADR-013). Dispatch is best-effort; the publisher swallows broker failures.
/// </summary>
public sealed class DomainEventsDispatchInterceptor(RabbitMqAuditEventPublisher publisher)
    : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToArray();

        foreach (var aggregate in aggregates)
        {
            var domainEvents = aggregate.DomainEvents.ToArray();
            aggregate.ClearDomainEvents();

            foreach (var domainEvent in domainEvents)
            {
                var message = SessionAuditEventMapper.Map(domainEvent);
                if (message is not null)
                {
                    await publisher.PublishAsync(message, cancellationToken);
                }
            }
        }
    }
}

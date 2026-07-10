using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SessionManagement.Domain.Abstractions;
using SessionManagement.Infrastructure.Messaging;

namespace SessionManagement.Infrastructure.Persistence;

/// <summary>
/// Publishes the audit events raised by tracked aggregates <b>inside</b> the business
/// transaction, via MassTransit's transactional outbox. Runs on the
/// <see cref="SaveChangesInterceptor.SavingChangesAsync"/> hook (before the rows are written):
/// each <see cref="IPublishEndpoint.Publish{T}(T,CancellationToken)"/> call is captured by the
/// EF Core bus outbox and staged as an <c>OutboxMessage</c> row on this same
/// <see cref="DbContext"/>, so the business change and the audit event commit atomically. A
/// background delivery service then relays the staged messages to RabbitMQ. This is the outbox
/// upgrade over the previous best-effort, post-commit publish (ADR-013).
/// <para>
/// Registered as a <b>scoped</b> interceptor so it shares the DbContext's scope with the scoped
/// <see cref="IPublishEndpoint"/> the outbox provides — that is what ties the staged messages to
/// this context's transaction.
/// </para>
/// <para>
/// <see cref="IPublishEndpoint"/> is resolved <b>lazily</b>, not constructor-injected: the outbox's
/// publish endpoint needs the <see cref="SessionManagementDbContext"/> to stage OutboxMessage rows,
/// while the DbContext needs this interceptor to build its options — constructor injection closes
/// that loop and deadlocks host startup. Resolving inside SavingChanges is safe because by then the
/// DbContext instance already exists in the scope.
/// </para>
/// </summary>
public sealed class DomainEventsDispatchInterceptor(IServiceProvider serviceProvider)
    : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        if (eventData.Context is not null)
        {
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var aggregates = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToArray();

        if (aggregates.Length == 0)
        {
            return;
        }

        var publishEndpoint = serviceProvider.GetRequiredService<IPublishEndpoint>();

        foreach (var aggregate in aggregates)
        {
            var domainEvents = aggregate.DomainEvents.ToArray();
            aggregate.ClearDomainEvents();

            foreach (var domainEvent in domainEvents)
            {
                var message = SessionAuditEventMapper.Map(domainEvent);
                if (message is not null)
                {
                    // With UseBusOutbox() active, this stages an OutboxMessage on `context`
                    // rather than hitting the broker; it is flushed with the current SaveChanges.
                    await publishEndpoint.Publish(message, cancellationToken);
                }
            }
        }
    }
}

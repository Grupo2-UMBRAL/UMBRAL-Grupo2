using ScoringMonitoring.Application.Abstractions;
using ScoringMonitoring.Domain.Audit;

namespace ScoringMonitoring.Application.Features.SessionEventLogs.Commands.LogSessionEvent;

public sealed class LogSessionEventHandler(
    IUnitOfWork unitOfWork,
    ISessionEventLogRepository sessionEventLogRepository,
    TimeProvider timeProvider,
    IScoringMonitoringUpdatesPublisher updatesPublisher)
    : IRequestHandler<LogSessionEventCommand, SessionEventLogPayload>
{
    public async Task<SessionEventLogPayload> Handle(
        LogSessionEventCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Idempotency: audit events flow over an at-least-once queue, so the same domain event
        // (identified by EventId) can be redelivered. When it carries an EventId we use it as the
        // log's identity and short-circuit on a duplicate rather than persisting the event twice.
        if (request.EventId is { } eventId)
        {
            var existing = await sessionEventLogRepository.GetByIdAsync(eventId, cancellationToken);
            if (existing is not null)
            {
                return SessionEventLogPayload.FromEntity(existing);
            }
        }

        var eventLog = new SessionEventLog(
            request.EventId ?? Guid.NewGuid(),
            request.LiveSessionId,
            request.EventType,
            request.Description,
            timeProvider.GetUtcNow());

        sessionEventLogRepository.Add(eventLog);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var payload = SessionEventLogPayload.FromEntity(eventLog);
        await updatesPublisher.PublishEventLogUpdatedAsync(payload, cancellationToken);

        return payload;
    }
}



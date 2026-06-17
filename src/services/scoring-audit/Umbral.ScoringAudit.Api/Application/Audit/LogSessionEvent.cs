using MediatR;
using Umbral.ScoringAudit.Api.Application.Scoreboards;
using Umbral.ScoringAudit.Api.Domain.Audit;
using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Application.Audit;

public sealed record LogSessionEventCommand(
    Guid LiveSessionId,
    string EventType,
    string Description) : IRequest<SessionEventLogPayload>
{
}

public sealed class LogSessionEventHandler(
    IScoringAuditDbContext dbContext,
    TimeProvider timeProvider,
    IScoringAuditUpdatesPublisher updatesPublisher)
    : IRequestHandler<LogSessionEventCommand, SessionEventLogPayload>
{
    public async Task<SessionEventLogPayload> Handle(
        LogSessionEventCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eventLog = new SessionEventLog(
            Guid.NewGuid(),
            request.LiveSessionId,
            request.EventType,
            request.Description,
            timeProvider.GetUtcNow());

        dbContext.SessionEventLogs.Add(eventLog);
        await dbContext.SaveChangesAsync(cancellationToken);

        var payload = SessionEventLogPayload.FromEntity(eventLog);
        await updatesPublisher.PublishEventLogUpdatedAsync(payload, cancellationToken);

        return payload;
    }
}

using Umbral.ScoringAudit.Api.Domain.Audit;

namespace Umbral.ScoringAudit.Api.Application.Audit;

public sealed record SessionEventLogPayload(
    Guid Id,
    Guid LiveSessionId,
    string EventType,
    string Description,
    DateTimeOffset Timestamp)
{
    public static SessionEventLogPayload FromEntity(SessionEventLog eventLog)
    {
        ArgumentNullException.ThrowIfNull(eventLog);

        return new SessionEventLogPayload(
            eventLog.Id,
            eventLog.LiveSessionId,
            eventLog.EventType,
            eventLog.Description,
            eventLog.Timestamp);
    }
}

public sealed record LogSessionEventRequest(string EventType, string Description);

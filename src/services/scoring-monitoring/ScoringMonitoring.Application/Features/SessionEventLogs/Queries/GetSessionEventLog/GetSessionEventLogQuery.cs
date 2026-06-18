namespace ScoringMonitoring.Application.Features.SessionEventLogs.Queries.GetSessionEventLog;

public sealed record GetSessionEventLogQuery(Guid LiveSessionId)
    : IRequest<IReadOnlyList<SessionEventLogPayload>>;

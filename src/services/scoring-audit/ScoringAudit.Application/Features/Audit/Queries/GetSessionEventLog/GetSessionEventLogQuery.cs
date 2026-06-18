namespace ScoringAudit.Application.Features.Audit.Queries.GetSessionEventLog;

public sealed record GetSessionEventLogQuery(Guid LiveSessionId)
    : IRequest<IReadOnlyList<SessionEventLogPayload>>;

namespace ScoringAudit.Application.Features.Audit.Commands.LogSessionEvent;

public sealed record LogSessionEventCommand(
    Guid LiveSessionId,
    string EventType,
    string Description) : IRequest<SessionEventLogPayload>;

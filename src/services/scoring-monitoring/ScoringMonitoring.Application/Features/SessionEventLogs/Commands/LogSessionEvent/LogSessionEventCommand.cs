namespace ScoringMonitoring.Application.Features.SessionEventLogs.Commands.LogSessionEvent;

public sealed record LogSessionEventCommand(
    Guid LiveSessionId,
    string EventType,
    string Description,
    Guid? EventId = null) : IRequest<SessionEventLogPayload>;


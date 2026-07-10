namespace ScoringMonitoring.Infrastructure.Messaging;

/// <summary>
/// scoring-monitoring's copy of the audit event wire shape. Deserialized from the JSON
/// published by session-management; kept structurally equal on purpose.
/// </summary>
public sealed record SessionAuditEventMessage(
    Guid EventId,
    Guid LiveSessionId,
    string EventType,
    string Description,
    DateTimeOffset OccurredAtUtc);

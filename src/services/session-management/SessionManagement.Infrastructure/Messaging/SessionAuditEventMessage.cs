namespace SessionManagement.Infrastructure.Messaging;

/// <summary>
/// Wire shape published to RabbitMQ (serialized as JSON). scoring-monitoring keeps
/// its own structurally-equal copy of this record and deserializes into it.
/// </summary>
public sealed record SessionAuditEventMessage(
    Guid EventId,
    Guid LiveSessionId,
    string EventType,
    string Description,
    DateTimeOffset OccurredAtUtc);

namespace Umbral.Contracts.Audit;

/// <summary>
/// Wire contract published to RabbitMQ when the LiveSession aggregate raises an auditable
/// domain event. scoring-monitoring keeps its own structurally-equal copy of this record.
/// <para>
/// The namespace is deliberately <c>Umbral.Contracts.Audit</c> in BOTH copies (not the owning
/// service's namespace): MassTransit derives the message exchange/URN from the type's
/// namespace + name (<c>urn:message:Umbral.Contracts.Audit:SessionAuditEventMessage</c>), so
/// publisher and consumer must share it for routing to line up. The source is still duplicated
/// per bounded context rather than shared via src/shared (ADR-012 keeps business contracts out
/// of the shared kernel) — only the logical contract name is kept identical.
/// </para>
/// </summary>
public sealed record SessionAuditEventMessage(
    Guid EventId,
    Guid LiveSessionId,
    string EventType,
    string Description,
    DateTimeOffset OccurredAtUtc);

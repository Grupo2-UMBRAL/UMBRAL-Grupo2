namespace Umbral.Contracts.Audit;

/// <summary>
/// scoring-monitoring's copy of the audit event wire contract. Consumed from the exchange
/// session-management publishes to; kept structurally equal on purpose.
/// <para>
/// The namespace is deliberately <c>Umbral.Contracts.Audit</c> in BOTH copies (not the owning
/// service's namespace): MassTransit matches the incoming message's URN
/// (<c>urn:message:Umbral.Contracts.Audit:SessionAuditEventMessage</c>) against this consumed
/// type, so it must be identical on both sides. The source is still duplicated per bounded
/// context rather than shared via src/shared (ADR-012 keeps business contracts out of the kernel).
/// </para>
/// </summary>
public sealed record SessionAuditEventMessage(
    Guid EventId,
    Guid LiveSessionId,
    string EventType,
    string Description,
    DateTimeOffset OccurredAtUtc);

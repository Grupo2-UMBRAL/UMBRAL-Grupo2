using SessionManagement.Domain.Abstractions;

namespace SessionManagement.Domain.LiveSessions;

/// <summary>
/// Auditable facts raised by the <see cref="LiveSession"/> aggregate. Each carries a fresh
/// <see cref="IDomainEvent.EventId"/> so a downstream consumer can dedupe on redelivery.
/// Fields are structured data only — the human-readable audit description is built in
/// Infrastructure (see SessionAuditEventMapper).
/// </summary>
public sealed record EvidenceSubmittedDomainEvent(
    Guid LiveSessionId,
    Guid EvidenceSubmissionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    string GameType,
    DateTimeOffset OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record EvidenceValidatedDomainEvent(
    Guid LiveSessionId,
    Guid EvidenceSubmissionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    string Outcome,
    string Source,
    DateTimeOffset OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record ValidationOutcomeOverriddenDomainEvent(
    Guid LiveSessionId,
    Guid EvidenceSubmissionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    string Outcome,
    string Reason,
    DateTimeOffset OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record HintReleasedDomainEvent(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    Guid HintId,
    string UnlockReason,
    DateTimeOffset OccurredOnUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

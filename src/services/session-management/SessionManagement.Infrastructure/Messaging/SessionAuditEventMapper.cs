using SessionManagement.Domain.Abstractions;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Infrastructure.Messaging;

/// <summary>
/// Translates a <see cref="IDomainEvent"/> raised by the LiveSession aggregate into the audit
/// wire message published to RabbitMQ. The presentation text (the human-readable description)
/// lives here in Infrastructure, not in the domain event. Returns <c>null</c> for events that
/// are not auditable, so the dispatcher can skip them.
/// </summary>
public static class SessionAuditEventMapper
{
    public static SessionAuditEventMessage? Map(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return domainEvent switch
        {
            EvidenceSubmittedDomainEvent submitted => new SessionAuditEventMessage(
                submitted.EventId,
                submitted.LiveSessionId,
                "EvidenceSubmitted",
                $"Session Team '{submitted.SessionTeamId}' submitted evidence for Mission Stage '{submitted.MissionStageId}'. Game Type: {submitted.GameType}.",
                submitted.OccurredOnUtc),

            EvidenceValidatedDomainEvent validated => new SessionAuditEventMessage(
                validated.EventId,
                validated.LiveSessionId,
                "ValidationOutcome",
                $"Evidence submission '{validated.EvidenceSubmissionId}' for Session Team '{validated.SessionTeamId}' on Mission Stage '{validated.MissionStageId}' was validated as {validated.Outcome}. Source: {validated.Source}.",
                validated.OccurredOnUtc),

            ValidationOutcomeOverriddenDomainEvent overridden => new SessionAuditEventMessage(
                overridden.EventId,
                overridden.LiveSessionId,
                "ValidationOutcome",
                $"Evidence submission '{overridden.EvidenceSubmissionId}' for Session Team '{overridden.SessionTeamId}' on Mission Stage '{overridden.MissionStageId}' was validated as {overridden.Outcome}. Source: OperatorOverride. Reason: {overridden.Reason}.",
                overridden.OccurredOnUtc),

            HintReleasedDomainEvent released => new SessionAuditEventMessage(
                released.EventId,
                released.LiveSessionId,
                "HintReleased",
                $"Hint '{released.HintId}' released to Session Team '{released.SessionTeamId}' for Mission Stage '{released.MissionStageId}'. Reason: {released.UnlockReason}.",
                released.OccurredOnUtc),

            _ => null,
        };
    }
}

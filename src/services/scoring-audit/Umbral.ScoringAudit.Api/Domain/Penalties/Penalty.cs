using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Domain.Penalties;

public enum PenaltySeverity
{
    Minor = -50,
    Major = -100,
    Critical = -200
}

public sealed class Penalty
{
    public const int ReasonMaximumLength = 500;
    public const int AppliedByOperatorUserIdMaximumLength = 120;

    public Penalty(
        Guid penaltyId,
        Guid commandId,
        Guid sessionTeamId,
        PenaltySeverity severity,
        string appliedByOperatorUserId,
        string reason,
        DateTimeOffset recordedAt)
    {
        if (penaltyId == Guid.Empty)
        {
            throw new UmbralDomainException("penalty.empty_id", "Penalty id is required.");
        }

        if (commandId == Guid.Empty)
        {
            throw new UmbralDomainException("penalty.empty_command_id", "Penalty command id is required.");
        }

        if (sessionTeamId == Guid.Empty)
        {
            throw new UmbralDomainException("penalty.empty_session_team_id", "Session Team id is required.");
        }

        if (!Enum.IsDefined(typeof(PenaltySeverity), severity))
        {
            throw new UmbralDomainException("penalty.unknown_severity", "Penalty Severity is not supported.");
        }

        if (string.IsNullOrWhiteSpace(appliedByOperatorUserId))
        {
            throw new UmbralDomainException("penalty.operator_user_id_required", "Operator user id is required.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new UmbralDomainException("penalty.reason_required", "Penalty reason is required.");
        }

        var normalizedAppliedByOperatorUserId = appliedByOperatorUserId.Trim();
        if (normalizedAppliedByOperatorUserId.Length > AppliedByOperatorUserIdMaximumLength)
        {
            throw new UmbralDomainException(
                "penalty.operator_user_id_too_long",
                $"Operator user id cannot exceed {AppliedByOperatorUserIdMaximumLength} characters.");
        }

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > ReasonMaximumLength)
        {
            throw new UmbralDomainException(
                "penalty.reason_too_long",
                $"Penalty reason cannot exceed {ReasonMaximumLength} characters.");
        }

        PenaltyId = penaltyId;
        CommandId = commandId;
        SessionTeamId = sessionTeamId;
        Severity = severity;
        AppliedByOperatorUserId = normalizedAppliedByOperatorUserId;
        Reason = normalizedReason;
        RecordedAt = recordedAt;
    }

    public Guid PenaltyId { get; }

    public Guid CommandId { get; }

    public Guid SessionTeamId { get; }

    public PenaltySeverity Severity { get; }

    public string AppliedByOperatorUserId { get; }

    public string Reason { get; }

    public DateTimeOffset RecordedAt { get; }

    public int Delta => (int)Severity;
}

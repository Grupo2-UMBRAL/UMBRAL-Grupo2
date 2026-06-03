using Umbral.ServiceDefaults;

namespace Umbral.SessionOperations.Api.Domain.LiveSessions;

public sealed class ValidationOverrideLog
{
    public const int OperatorUserIdMaximumLength = 120;
    public const int ReasonMaximumLength = 500;

    private ValidationOverrideLog()
    {
    }

    private ValidationOverrideLog(
        Guid id,
        Guid liveSessionId,
        Guid evidenceSubmissionId,
        Guid sessionTeamId,
        Guid missionStageId,
        string operatorUserId,
        string reason,
        ValidationOutcome previousOutcome,
        ValidationOutcome newOutcome,
        DateTimeOffset overriddenAtUtc)
    {
        Id = id;
        LiveSessionId = liveSessionId;
        EvidenceSubmissionId = evidenceSubmissionId;
        SessionTeamId = sessionTeamId;
        MissionStageId = missionStageId;
        OperatorUserId = operatorUserId;
        Reason = reason;
        PreviousOutcome = previousOutcome;
        NewOutcome = newOutcome;
        OverriddenAtUtc = overriddenAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid EvidenceSubmissionId { get; private set; }

    public Guid SessionTeamId { get; private set; }

    public Guid MissionStageId { get; private set; }

    public string OperatorUserId { get; private set; } = string.Empty;

    public string Reason { get; private set; } = string.Empty;

    public ValidationOutcome PreviousOutcome { get; private set; }

    public ValidationOutcome NewOutcome { get; private set; }

    public DateTimeOffset OverriddenAtUtc { get; private set; }

    public static ValidationOverrideLog Create(
        Guid liveSessionId,
        EvidenceSubmission evidenceSubmission,
        string operatorUserId,
        string reason,
        ValidationOutcome previousOutcome,
        ValidationOutcome newOutcome,
        DateTimeOffset overriddenAtUtc)
    {
        ArgumentNullException.ThrowIfNull(evidenceSubmission);

        return new ValidationOverrideLog(
            Guid.NewGuid(),
            NormalizeGuid(liveSessionId, "validation_override_live_session_required", "Validation Override must belong to a LiveSession."),
            NormalizeGuid(evidenceSubmission.Id, "validation_override_submission_required", "Validation Override must reference an Evidence Submission."),
            NormalizeGuid(evidenceSubmission.SessionTeamId, "validation_override_session_team_required", "Validation Override must reference a Session Team."),
            NormalizeGuid(evidenceSubmission.MissionStageId, "validation_override_stage_required", "Validation Override must reference a Mission Stage."),
            NormalizeRequiredText(operatorUserId, "validation_override_operator_required", "Operator identity is required.", OperatorUserIdMaximumLength),
            NormalizeRequiredText(reason, "validation_override_reason_required", "Validation Override reason is required.", ReasonMaximumLength),
            previousOutcome,
            newOutcome,
            overriddenAtUtc);
    }

    private static Guid NormalizeGuid(Guid value, string errorCode, string errorMessage)
    {
        if (value == Guid.Empty)
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
        }

        return value;
    }

    private static string NormalizeRequiredText(
        string value,
        string errorCode,
        string errorMessage,
        int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                $"{errorCode}_too_long",
                $"Value cannot exceed {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }
}

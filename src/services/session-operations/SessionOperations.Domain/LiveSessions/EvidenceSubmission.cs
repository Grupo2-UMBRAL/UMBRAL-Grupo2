using Umbral.ServiceDefaults;

namespace SessionOperations.Domain.LiveSessions;

public sealed class EvidenceSubmission
{
    public const int SubmittedHashMaximumLength = 512;
    public const int SubmittedTextMaximumLength = 1000;
    public const int FailureReasonMaximumLength = 120;

    private EvidenceSubmission()
    {
    }

    private EvidenceSubmission(
        Guid id,
        Guid liveSessionId,
        Guid sessionTeamId,
        Guid missionStageId,
        string gameType,
        string? submittedHash,
        string? submittedText,
        ValidationOutcome outcome,
        string? failureReason,
        DateTimeOffset submittedAtUtc)
    {
        Id = id;
        LiveSessionId = liveSessionId;
        SessionTeamId = sessionTeamId;
        MissionStageId = missionStageId;
        GameType = gameType;
        SubmittedHash = submittedHash;
        SubmittedText = submittedText;
        Outcome = outcome;
        FailureReason = failureReason;
        SubmittedAtUtc = submittedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid SessionTeamId { get; private set; }

    public Guid MissionStageId { get; private set; }

    public string GameType { get; private set; } = string.Empty;

    public string? SubmittedHash { get; private set; }

    public string? SubmittedText { get; private set; }

    public ValidationOutcome Outcome { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }

    public static EvidenceSubmission Create(
        Guid liveSessionId,
        Guid sessionTeamId,
        LiveSessionStage stage,
        string submittedHash,
        ValidationOutcome outcome,
        string? failureReason,
        DateTimeOffset submittedAtUtc)
        => CreateTreasureHunt(
            liveSessionId,
            sessionTeamId,
            stage,
            submittedHash,
            outcome,
            failureReason,
            submittedAtUtc);

    public static EvidenceSubmission CreateTreasureHunt(
        Guid liveSessionId,
        Guid sessionTeamId,
        LiveSessionStage stage,
        string submittedHash,
        ValidationOutcome outcome,
        string? failureReason,
        DateTimeOffset submittedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(stage);

        return new EvidenceSubmission(
            Guid.NewGuid(),
            NormalizeGuid(liveSessionId, "evidence_submission_live_session_required", "Evidence Submission must belong to a LiveSession."),
            NormalizeGuid(sessionTeamId, "evidence_submission_session_team_required", "Evidence Submission must belong to a Session Team."),
            NormalizeGuid(stage.MissionStageId, "evidence_submission_stage_required", "Evidence Submission must reference a Mission Stage."),
            NormalizeRequiredText(stage.GameType, "evidence_submission_game_type_required", "Evidence Submission game type is required.", 40),
            NormalizeRequiredText(submittedHash, "evidence_submission_hash_required", "Evidence Submission QR hash is required.", SubmittedHashMaximumLength),
            null,
            outcome,
            NormalizeOptionalText(failureReason, FailureReasonMaximumLength),
            submittedAtUtc);
    }

    public static EvidenceSubmission CreateTrivia(
        Guid liveSessionId,
        Guid sessionTeamId,
        LiveSessionStage stage,
        string submittedText,
        ValidationOutcome outcome,
        string? failureReason,
        DateTimeOffset submittedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(stage);

        return new EvidenceSubmission(
            Guid.NewGuid(),
            NormalizeGuid(liveSessionId, "evidence_submission_live_session_required", "Evidence Submission must belong to a LiveSession."),
            NormalizeGuid(sessionTeamId, "evidence_submission_session_team_required", "Evidence Submission must belong to a Session Team."),
            NormalizeGuid(stage.MissionStageId, "evidence_submission_stage_required", "Evidence Submission must reference a Mission Stage."),
            NormalizeRequiredText(stage.GameType, "evidence_submission_game_type_required", "Evidence Submission game type is required.", 40),
            null,
            NormalizeRequiredText(submittedText, "evidence_submission_text_required", "Evidence Submission answer text is required.", SubmittedTextMaximumLength),
            outcome,
            NormalizeOptionalText(failureReason, FailureReasonMaximumLength),
            submittedAtUtc);
    }

    public void ApplyOverride(ValidationOutcome newOutcome, string? failureReason)
    {
        Outcome = newOutcome;
        FailureReason = NormalizeOptionalText(failureReason, FailureReasonMaximumLength);
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

    private static string? NormalizeOptionalText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new UmbralDomainException(
                "evidence_submission_failure_reason_too_long",
                $"Value cannot exceed {maximumLength} characters.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }
}

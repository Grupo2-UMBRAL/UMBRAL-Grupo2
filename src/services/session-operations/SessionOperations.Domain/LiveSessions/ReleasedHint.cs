using Umbral.ServiceDefaults;

namespace SessionOperations.Domain.LiveSessions;

public sealed class ReleasedHint
{
    public const int UnlockReasonMaximumLength = 20;

    private ReleasedHint()
    {
    }

    private ReleasedHint(
        Guid id,
        Guid liveSessionId,
        Guid sessionTeamId,
        Guid missionStageId,
        Guid hintId,
        DateTimeOffset releasedAtUtc,
        string unlockReason)
    {
        Id = id;
        LiveSessionId = liveSessionId;
        SessionTeamId = sessionTeamId;
        MissionStageId = missionStageId;
        HintId = hintId;
        ReleasedAtUtc = releasedAtUtc;
        UnlockReason = unlockReason;
    }

    public Guid Id { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid SessionTeamId { get; private set; }

    public Guid MissionStageId { get; private set; }

    public Guid HintId { get; private set; }

    public DateTimeOffset ReleasedAtUtc { get; private set; }

    public string UnlockReason { get; private set; } = string.Empty;

    public static ReleasedHint Create(
        Guid liveSessionId,
        Guid sessionTeamId,
        Guid missionStageId,
        Guid hintId,
        DateTimeOffset releasedAtUtc,
        string unlockReason)
    {
        if (liveSessionId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "released_hint_live_session_required",
                "Released Hint must reference a LiveSession.",
                UmbralFailureCategory.Validation);
        }

        if (sessionTeamId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "released_hint_session_team_required",
                "Released Hint must reference a Session Team.",
                UmbralFailureCategory.Validation);
        }

        if (missionStageId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "released_hint_mission_stage_required",
                "Released Hint must reference a Mission Stage.",
                UmbralFailureCategory.Validation);
        }

        if (hintId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "released_hint_hint_required",
                "Released Hint must reference a Hint.",
                UmbralFailureCategory.Validation);
        }

        var normalizedUnlockReason = NormalizeUnlockReason(unlockReason);

        return new ReleasedHint(
            Guid.NewGuid(),
            liveSessionId,
            sessionTeamId,
            missionStageId,
            hintId,
            releasedAtUtc,
            normalizedUnlockReason);
    }

    private static string NormalizeUnlockReason(string unlockReason)
    {
        if (string.IsNullOrWhiteSpace(unlockReason))
        {
            throw new UmbralDomainException(
                "released_hint_unlock_reason_required",
                "Released Hint unlock reason is required.",
                UmbralFailureCategory.Validation);
        }

        var normalized = unlockReason.Trim();
        if (!string.Equals(normalized, "Manual", StringComparison.Ordinal)
            && !string.Equals(normalized, "Rule", StringComparison.Ordinal))
        {
            throw new UmbralDomainException(
                "released_hint_unlock_reason_invalid",
                "Released Hint unlock reason must be Manual or Rule.",
                UmbralFailureCategory.Validation);
        }

        return normalized;
    }
}

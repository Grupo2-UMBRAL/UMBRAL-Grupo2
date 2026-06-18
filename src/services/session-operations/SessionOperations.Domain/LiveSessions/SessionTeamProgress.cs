using Umbral.ServiceDefaults;

namespace SessionOperations.Domain.LiveSessions;

public static class SessionTeamProgressStates
{
    public const string NotStarted = "NotStarted";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
}

public sealed class SessionTeamProgress
{
    private SessionTeamProgress()
    {
    }

    private SessionTeamProgress(
        Guid id,
        Guid liveSessionId,
        Guid sessionTeamId,
        int currentStageIndex,
        string state,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        LiveSessionId = liveSessionId;
        SessionTeamId = sessionTeamId;
        CurrentStageIndex = currentStageIndex;
        State = state;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid LiveSessionId { get; private set; }

    public Guid SessionTeamId { get; private set; }

    public int CurrentStageIndex { get; private set; }

    public string State { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static SessionTeamProgress Create(Guid liveSessionId, Guid sessionTeamId, DateTimeOffset startedAtUtc)
        => new(
            Guid.NewGuid(),
            NormalizeGuid(liveSessionId, "session_team_progress_live_session_required", "Session Team Progress must belong to a LiveSession."),
            NormalizeGuid(sessionTeamId, "session_team_progress_session_team_required", "Session Team Progress must belong to a Session Team."),
            0,
            SessionTeamProgressStates.InProgress,
            startedAtUtc);

    public void AdvanceTo(int nextStageIndex, DateTimeOffset updatedAtUtc)
    {
        if (nextStageIndex < 0)
        {
            throw new UmbralDomainException(
                "session_team_progress_stage_index_invalid",
                "Session Team Progress stage index cannot be negative.",
                UmbralFailureCategory.Validation);
        }

        CurrentStageIndex = nextStageIndex;
        State = SessionTeamProgressStates.InProgress;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Complete(DateTimeOffset completedAtUtc)
    {
        State = SessionTeamProgressStates.Completed;
        UpdatedAtUtc = completedAtUtc;
    }

    private static Guid NormalizeGuid(Guid value, string errorCode, string errorMessage)
    {
        if (value == Guid.Empty)
        {
            throw new UmbralDomainException(errorCode, errorMessage, UmbralFailureCategory.Validation);
        }

        return value;
    }
}

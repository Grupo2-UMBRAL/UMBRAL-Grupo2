using System.Text.Json.Serialization;

namespace SessionManagement.Application.Features.SessionSnapshots;

public sealed record LiveSessionOverview(
    Guid LiveSessionId,
    string Name,
    Guid MissionId,
    string MissionName,
    string SessionState,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ScheduledStartAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RemainingSeconds,
    DateTimeOffset ServerTimeUtc,
    SnapshotSyncMetadata Sync,
    IReadOnlyList<LiveSessionOverviewTeam> SessionTeams);

public sealed record LiveSessionOverviewTeam(
    Guid SessionTeamId,
    string TeamName,
    int ParticipantCount,
    string ProgressState,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CurrentSessionStageSnapshot? CurrentStage,
    IReadOnlyList<VisibleHintSnapshot> ReleasedHints);

public sealed record SessionTeamSnapshot(
    Guid LiveSessionId,
    Guid SessionTeamId,
    string TeamName,
    string SessionState,
    string ProgressState,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CurrentSessionStageSnapshot? CurrentStage,
    IReadOnlyList<VisibleHintSnapshot> VisibleHints,
    SnapshotSyncMetadata Sync,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<CurrentSessionStageSnapshot>? AllStages,
    // MemberCount powers the lobby roster; TotalStages powers the linear stage progress. Both are
    // safe to expose during play (no future stage content is revealed, only the count).
    int MemberCount = 0,
    int TotalStages = 0);

// Participant-facing projection of the current Play. It intentionally exposes
// only the selectable Choices (id + text). The correct choice id and any
// is-correct flag are NEVER included here — Trivia validation stays 100%
// server-side. Treasure Hunt plays carry an empty Choices list.
public sealed record CurrentSessionStageSnapshot(
    Guid MissionStageId,
    string Name,
    int SessionStageOrder,
    int SourceOrder,
    int ResolvedTimeBudgetMinutes,
    string Difficulty,
    string GameType,
    string Prompt,
    IReadOnlyList<SessionStageChoiceSnapshot> Choices);

public sealed record SessionStageChoiceSnapshot(
    Guid Id,
    string Text);

public sealed record VisibleHintSnapshot(
    Guid HintId,
    Guid MissionStageId,
    string Content,
    bool IsSolution,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Latitude,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Longitude,
    DateTimeOffset UnlockedAtUtc,
    string UnlockReason);

public sealed record SnapshotSyncMetadata(
    long SequenceNumber,
    DateTimeOffset LastUpdatedUtc,
    DateTimeOffset ServerTimeUtc);

public static class SessionSnapshotConstants
{
    public const long InitialSequenceNumber = 0;
    public const string NotStartedProgressState = "NotStarted";
}


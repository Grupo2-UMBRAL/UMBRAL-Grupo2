using System.Text.Json.Serialization;

namespace Umbral.SessionOperations.Api.Application.SessionSnapshots;

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
    SnapshotSyncMetadata Sync);

public sealed record CurrentSessionStageSnapshot(
    Guid MissionStageId,
    string Name,
    int SessionStageOrder,
    int SourceOrder,
    int ResolvedTimeBudgetMinutes,
    string Difficulty,
    string GameType);

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

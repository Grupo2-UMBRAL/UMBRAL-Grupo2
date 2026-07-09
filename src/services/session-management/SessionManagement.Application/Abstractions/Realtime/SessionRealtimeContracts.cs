using System.Text.Json.Serialization;
using SessionManagement.Application.Features.SessionSnapshots;

namespace SessionManagement.Application.Abstractions.Realtime;

public enum SnapshotRefreshPolicy
{
    ApplyIncremental = 1,
    RefreshSnapshot = 2
}

public sealed record RealtimeEventMetadata(
    Guid LiveSessionId,
    long SequenceNumber,
    DateTimeOffset OccurredAtUtc,
    SnapshotRefreshPolicy RefreshPolicy,
    string Reason);

public sealed record SessionStateChangedPayload(
    RealtimeEventMetadata Metadata,
    string PreviousState,
    string CurrentState,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RemainingSeconds);

public sealed record TeamProgressChangedPayload(
    RealtimeEventMetadata Metadata,
    Guid SessionTeamId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CurrentSessionStageSnapshot? PreviousStage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CurrentSessionStageSnapshot? CurrentStage,
    string ProgressState);

public sealed record EvidenceSubmissionOutcomeChangedPayload(
    RealtimeEventMetadata Metadata,
    Guid EvidenceSubmissionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    string CurrentOutcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PreviousOutcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FailureReason);

public sealed record HintUnlockedPayload(
    RealtimeEventMetadata Metadata,
    Guid SessionTeamId,
    VisibleHintSnapshot Hint);

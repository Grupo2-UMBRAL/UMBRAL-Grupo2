using System.Text.Json.Serialization;

namespace SessionManagement.Application.Features.SessionSnapshots;

/// <summary>
/// Operator dashboard view of a running LiveSession: the session's own state plus one row per
/// Session Team, each on its own Play under Per-Team Progression.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session being watched.</param>
/// <param name="Name" example="Friday night run">Operator-visible name of this execution.</param>
/// <param name="MissionId" example="7c3a1b9e-2f4d-4c8a-9b1e-6d5f0a3c8b21">Mission this execution was snapshotted from.</param>
/// <param name="MissionName" example="Downtown hunt">Mission name captured at snapshot time.</param>
/// <param name="SessionState" example="Active">Session State: Scheduled, Active, Paused, Finalized or Canceled. Decides whether advances and evidence are accepted right now.</param>
/// <param name="ScheduledStartAtUtc" example="2026-07-16T21:00:00Z">Announced start instant. Omitted when the session has no schedule.</param>
/// <param name="RemainingSeconds" example="900">Seconds left until the announced start, for the pre-start countdown. Only present while the session is Scheduled with a future start instant; omitted once it starts or when there is nothing to count down to.</param>
/// <param name="ServerTimeUtc" example="2026-07-16T20:45:00Z">Server clock at projection time. Clients should render countdowns against this instead of the device clock.</param>
/// <param name="Sync">Ordering metadata used to reconcile this snapshot with realtime events.</param>
/// <param name="SessionTeams">Teams in the session, ordered by name. Empty until participants enrol.</param>
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

/// <summary>
/// One Session Team's line in the operator overview.
/// </summary>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Team this row describes.</param>
/// <param name="TeamName" example="Los Topos">Name participants chose for the team.</param>
/// <param name="ParticipantCount" example="4">Participants currently playing in the team.</param>
/// <param name="ProgressState" example="InProgress">NotStarted, InProgress or Completed. Position of this team in the Session Flow, independent of every other team.</param>
/// <param name="CurrentStage">Play the team is working on. Omitted when the team has not started or has finished the Session Flow, so its absence is what distinguishes those states from live play.</param>
/// <param name="ReleasedHints">Hints already unlocked for this team, so the operator can see what it has been told. Solution Hints stay out until the session is Finalized.</param>
public sealed record LiveSessionOverviewTeam(
    Guid SessionTeamId,
    string TeamName,
    int ParticipantCount,
    string ProgressState,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CurrentSessionStageSnapshot? CurrentStage,
    IReadOnlyList<VisibleHintSnapshot> ReleasedHints);

/// <summary>
/// The Participant Stage View: everything the mobile app of one Session Team may know at this
/// instant. It never reveals future Plays' content nor which Trivia choice is correct.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the team plays in.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Team this view belongs to. Only its own participants may read it.</param>
/// <param name="TeamName" example="Los Topos">Name of the team.</param>
/// <param name="SessionState" example="Active">Session State: Scheduled, Active, Paused, Finalized or Canceled. Tells the app whether play is currently allowed.</param>
/// <param name="ProgressState" example="InProgress">NotStarted, InProgress or Completed for this team alone.</param>
/// <param name="CurrentStage">Play to render now. Omitted when the team has not started or already finished the Session Flow.</param>
/// <param name="VisibleHints">Hints unlocked for this team so far. Solution Hints appear only once the session is Finalized.</param>
/// <param name="Sync">Ordering metadata used to reconcile this snapshot with realtime events.</param>
/// <param name="AllStages">Full Session Flow, revealed only once the session is Finalized so teams can review the run. Omitted while the session is still playable, precisely to avoid leaking upcoming Plays.</param>
/// <param name="MemberCount" example="4">Participants in this team; powers the lobby roster.</param>
/// <param name="TotalStages" example="8">Plays in the Session Flow. Powers the linear progress indicator without revealing any future Play's content.</param>
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

/// <summary>
/// Participant-facing projection of a Play. It carries no solution data: no correct choice id, no
/// is-correct flag and no expected QR hash, because Trivia validation stays server-side.
/// </summary>
/// <param name="MissionStageId" example="2b8c47f1-9a3e-4d56-b7c8-1e9f0a2d3b64">Play identifier, inherited from the Mission.</param>
/// <param name="Name" example="Play 3">Label of the Play.</param>
/// <param name="SessionStageOrder" example="1">Contiguous 1-based position in this Session Flow; pair it with the flow's total to show progress.</param>
/// <param name="SourceOrder" example="4">Original position of the Play in the Mission, kept for traceability. It may skip numbers when only some Plays were selected.</param>
/// <param name="ResolvedTimeBudgetMinutes" example="15">Time limit resolved at snapshot time; drives the on-screen timer.</param>
/// <param name="Difficulty" example="Medium">Easy, Medium or Hard. Signals what the Play is worth once scored.</param>
/// <param name="GameType" example="Trivia">Trivia or Treasure Hunt. Decides whether the app shows the choices or the QR scanner, and which evidence endpoint to call.</param>
/// <param name="Prompt" example="Which year was the clock tower built?">Main text to display: the question in Trivia, the instruction or objective in Treasure Hunt.</param>
/// <param name="Choices">Selectable alternatives on a Trivia Play. Empty on Treasure Hunt, where evidence is a scanned QR instead.</param>
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

/// <summary>
/// A selectable alternative of the current Trivia Play, as offered to participants.
/// </summary>
/// <param name="Id" example="4d6e2a10-7c95-4b83-a1f2-8e0d9c7b6a53">Value to send back as the selected choice when submitting the answer.</param>
/// <param name="Text" example="1897">Alternative as displayed. Carries no mark of correctness.</param>
public sealed record SessionStageChoiceSnapshot(
    Guid Id,
    string Text);

/// <summary>
/// A Hint already unlocked for a Session Team, with the trace of why it became visible.
/// </summary>
/// <param name="HintId" example="9a0b1c2d-3e4f-4a5b-8c6d-7e8f9a0b1c2d">The Hint that was released.</param>
/// <param name="MissionStageId" example="2b8c47f1-9a3e-4d56-b7c8-1e9f0a2d3b64">Play the Hint belongs to, so clients can attach it to the right Play.</param>
/// <param name="Content" example="Look behind the fountain">Text delivered to the team.</param>
/// <param name="IsSolution">true = the Hint reveals the answer; it only ever reaches participants once the session is Finalized.</param>
/// <param name="Latitude" example="-34.603722">Latitude of the place the Hint points to. Omitted when the Hint has no location and is text only.</param>
/// <param name="Longitude" example="-58.381592">Longitude of the place the Hint points to. Omitted when the Hint has no location and is text only.</param>
/// <param name="UnlockedAtUtc" example="2026-07-16T21:12:30Z">Instant the Hint became visible to this team.</param>
/// <param name="UnlockReason" example="Manual">Manual = an operator released it deliberately; Rule = it opened automatically. Keeps Hint Release auditable.</param>
public sealed record VisibleHintSnapshot(
    Guid HintId,
    Guid MissionStageId,
    string Content,
    bool IsSolution,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Latitude,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Longitude,
    DateTimeOffset UnlockedAtUtc,
    string UnlockReason);

/// <summary>
/// Ordering metadata that lets a client tell whether a snapshot is older than the realtime events
/// it has already applied.
/// </summary>
/// <param name="SequenceNumber" example="42">Monotonic counter of the LiveSession. Discard a snapshot whose number is lower than the last event already applied.</param>
/// <param name="LastUpdatedUtc" example="2026-07-16T21:12:30Z">Instant of the newest fact reflected here, not the instant the response was built.</param>
/// <param name="ServerTimeUtc" example="2026-07-16T21:13:00Z">Server clock at projection time. Use it to age timers instead of trusting the device clock.</param>
public sealed record SnapshotSyncMetadata(
    long SequenceNumber,
    DateTimeOffset LastUpdatedUtc,
    DateTimeOffset ServerTimeUtc);

public static class SessionSnapshotConstants
{
    public const long InitialSequenceNumber = 0;
    public const string NotStartedProgressState = "NotStarted";
}


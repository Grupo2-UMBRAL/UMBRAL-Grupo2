using MediatR;

namespace SessionManagement.Application.Features.SessionSnapshots;

public sealed record GetSessionTeamDetailQuery(
    Guid LiveSessionId,
    Guid SessionTeamId,
    int InactivityThresholdMinutes = 10) : IRequest<SessionTeamDetailResponse>;

/// <summary>
/// Operator drill-down into one Session Team: its current Play plus the full history of Hints it
/// received and attempts it made, so an operator can judge an ambiguous case before overriding it.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the team plays in.</param>
/// <param name="LiveSessionName" example="Friday night run">Operator-visible name of the execution.</param>
/// <param name="SessionState" example="Active">Session State: Scheduled, Active, Paused, Finalized or Canceled.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Team being inspected.</param>
/// <param name="TeamName" example="Los Topos">Name of the team.</param>
/// <param name="ParticipantCount" example="4">Participants currently playing in the team.</param>
/// <param name="ProgressState" example="InProgress">NotStarted, InProgress or Completed for this team alone.</param>
/// <param name="CurrentStage">Play the team is on. null when it has not started or has finished the Session Flow.</param>
/// <param name="CurrentStageStartedAtUtc" example="2026-07-16T21:05:00Z">When the team reached its current Play; the baseline for how long it has been stuck. Falls back to the session's scheduled start, then its creation instant, for a team that never advanced.</param>
/// <param name="ReleasedHints">Every Hint released to this team, newest last. Includes solution Hints: this is an operator view.</param>
/// <param name="EvidenceSubmissions">Every attempt the team made, accepted or not.</param>
/// <param name="IsInactive">true = neither an attempt nor a stage advance happened within the threshold below, flagging a team that may be stuck and worth a Hint.</param>
/// <param name="InactivityThresholdMinutes" example="10">Threshold actually used to compute IsInactive. Echoed because values below 1 are clamped up to 1.</param>
/// <param name="ServerTimeUtc" example="2026-07-16T21:20:00Z">Server clock at projection time; the reference the inactivity check was measured against.</param>
/// <param name="Sync">Ordering metadata used to reconcile this snapshot with realtime events.</param>
public sealed record SessionTeamDetailResponse(
    Guid LiveSessionId,
    string LiveSessionName,
    string SessionState,
    Guid SessionTeamId,
    string TeamName,
    int ParticipantCount,
    string ProgressState,
    CurrentSessionStageSnapshot? CurrentStage,
    DateTimeOffset CurrentStageStartedAtUtc,
    IReadOnlyList<SessionTeamReleasedHintDetail> ReleasedHints,
    IReadOnlyList<SessionTeamEvidenceSubmissionDetail> EvidenceSubmissions,
    bool IsInactive,
    int InactivityThresholdMinutes,
    DateTimeOffset ServerTimeUtc,
    SnapshotSyncMetadata Sync);

/// <summary>
/// One Hint Release in a Session Team's history, as an operator sees it.
/// </summary>
/// <param name="ReleasedHintId" example="1e5b8d20-3f7c-4a96-8b41-9d2e0c6f5a37">The release event itself, distinct from the Hint: the same Hint released to several teams yields one of these each.</param>
/// <param name="HintId" example="9a0b1c2d-3e4f-4a5b-8c6d-7e8f9a0b1c2d">Hint that was released.</param>
/// <param name="MissionStageId" example="2b8c47f1-9a3e-4d56-b7c8-1e9f0a2d3b64">Play the Hint belongs to.</param>
/// <param name="StageName" example="Play 3">Label of that Play, so the history reads without a second lookup.</param>
/// <param name="Content" example="Look behind the fountain">Text the team received.</param>
/// <param name="IsSolution">true = the Hint reveals the answer. Visible to the operator here even while it stays hidden from the team.</param>
/// <param name="Latitude" example="-34.603722">Latitude of the place the Hint points to. null = a text-only Hint with no location.</param>
/// <param name="Longitude" example="-58.381592">Longitude of the place the Hint points to. null = a text-only Hint with no location.</param>
/// <param name="ReleasedAtUtc" example="2026-07-16T21:12:30Z">Instant the Hint reached the team.</param>
/// <param name="UnlockReason" example="Manual">Manual = an operator released it deliberately; Rule = it opened automatically.</param>
public sealed record SessionTeamReleasedHintDetail(
    Guid ReleasedHintId,
    Guid HintId,
    Guid MissionStageId,
    string StageName,
    string Content,
    bool IsSolution,
    decimal? Latitude,
    decimal? Longitude,
    DateTimeOffset ReleasedAtUtc,
    string UnlockReason);

/// <summary>
/// One Evidence Submission in a Session Team's history, with the game-specific evidence it carried.
/// </summary>
/// <param name="Id" example="8c1f3b57-4e29-4d6a-b083-2f9e7c5d1a48">The attempt. Pass it to the override endpoint to correct this result.</param>
/// <param name="MissionStageId" example="2b8c47f1-9a3e-4d56-b7c8-1e9f0a2d3b64">Play the attempt was made on.</param>
/// <param name="StageName" example="Play 3">Label of that Play. Reads "Unknown Session Stage" if the Play is no longer in the Session Flow.</param>
/// <param name="SessionStageOrder" example="1">Position of that Play in the Session Flow; 0 when the Play can no longer be resolved.</param>
/// <param name="Difficulty" example="Medium">Easy, Medium or Hard, as snapshotted for that Play.</param>
/// <param name="GameType" example="Trivia">Trivia or Treasure Hunt. Tells which of the three evidence fields below is filled.</param>
/// <param name="SubmittedHash" example="9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08">Hash of the scanned QR on a Treasure Hunt attempt. null on Trivia.</param>
/// <param name="SubmittedText">Free-text evidence retained from earlier submissions. null on current Trivia and Treasure Hunt attempts, which never send free text.</param>
/// <param name="SubmittedChoiceId" example="4d6e2a10-7c95-4b83-a1f2-8e0d9c7b6a53">Alternative the team picked on a Trivia attempt. null on Treasure Hunt.</param>
/// <param name="ValidationOutcome" example="Rejected">Accepted or Rejected. Reflects any Validation Override already applied.</param>
/// <param name="FailureReason" example="QR hash did not match the expected value">Why the attempt was rejected. null when it was accepted.</param>
/// <param name="SubmittedAtUtc" example="2026-07-16T21:14:05Z">Backend reception instant, the official Resolution Time reference.</param>
/// <param name="IsTriviaCorrectionEligible">true = a rejected Trivia attempt, the only case a Validation Override is meant for. Lets the console offer the action only where it applies.</param>
public sealed record SessionTeamEvidenceSubmissionDetail(
    Guid Id,
    Guid MissionStageId,
    string StageName,
    int SessionStageOrder,
    string Difficulty,
    string GameType,
    string? SubmittedHash,
    string? SubmittedText,
    Guid? SubmittedChoiceId,
    string ValidationOutcome,
    string? FailureReason,
    DateTimeOffset SubmittedAtUtc,
    bool IsTriviaCorrectionEligible);


using MediatR;

namespace SessionManagement.Application.Features.SessionSnapshots;

public sealed record GetSessionTeamDetailQuery(
    Guid LiveSessionId,
    Guid SessionTeamId,
    int InactivityThresholdMinutes = 10) : IRequest<SessionTeamDetailResponse>;

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


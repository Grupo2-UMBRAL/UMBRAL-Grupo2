namespace SessionOperations.Application.Scoring;

public interface IScoringAuditClient
{
    Task RecordStageCreditAsync(
        RecordStageCreditRequest request,
        CancellationToken cancellationToken);

    Task<ApplyPenaltyResponse> ApplyPenaltyAsync(
        ApplyPenaltyRequest request,
        CancellationToken cancellationToken);

    Task LogSessionEventAsync(
        Guid liveSessionId,
        string eventType,
        string description,
        CancellationToken cancellationToken);
}

public sealed record RecordStageCreditRequest(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    string Difficulty,
    TimeSpan ResolutionTime,
    DateTimeOffset RecordedAt,
    bool ValidationOverride);

public sealed record ApplyPenaltyRequest(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string AppliedByOperatorUserId,
    string Reason,
    DateTimeOffset RecordedAt);

public sealed record ApplyPenaltyResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    Guid? PenaltyId,
    Guid? ScoreEntryId,
    bool PenaltyApplied,
    int VisibleScore,
    RankingPayload Ranking);

public sealed record RankingPayload(
    Guid LiveSessionId,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<RankingItem> Items);

public sealed record RankingItem(
    int Rank,
    Guid SessionTeamId,
    int VisibleScore,
    TimeSpan ResolutionTime);

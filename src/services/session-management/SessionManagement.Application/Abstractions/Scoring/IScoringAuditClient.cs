namespace SessionManagement.Application.Abstractions.Scoring;

public interface IScoringMonitoringClient
{
    Task RecordStageCreditAsync(
        RecordStageCreditRequest request,
        CancellationToken cancellationToken);

    Task<ApplyPenaltyResponse> ApplyPenaltyAsync(
        ApplyPenaltyRequest request,
        CancellationToken cancellationToken);
}

public sealed record RecordStageCreditRequest(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid PlayId,
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

/// <summary>
/// Ranking of a LiveSession's Session Teams, as projected by Scoring and Monitoring.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session this ranking covers.</param>
/// <param name="GeneratedAtUtc" example="2026-07-16T21:25:00Z">Instant the projection was computed. The ranking is a snapshot, not a live figure.</param>
/// <param name="Items">Teams ordered by rank. Empty until at least one team has been scored.</param>
public sealed record RankingPayload(
    Guid LiveSessionId,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<RankingItem> Items);

/// <summary>
/// One Session Team's position in the ranking.
/// </summary>
/// <param name="Rank" example="1">1-based position, highest score first.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Ranked team.</param>
/// <param name="VisibleScore" example="120">Accumulated score shown to participants, penalties included.</param>
/// <param name="ResolutionTime" example="00:12:30.500">Official resolution time, measured at 500 ms precision. It breaks ties between equal scores and never adds points of its own.</param>
public sealed record RankingItem(
    int Rank,
    Guid SessionTeamId,
    int VisibleScore,
    TimeSpan ResolutionTime);

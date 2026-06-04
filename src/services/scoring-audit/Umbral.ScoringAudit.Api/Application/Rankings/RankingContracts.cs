namespace Umbral.ScoringAudit.Api.Application.Rankings;

public sealed record RankingPayload(
    Guid LiveSessionId,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<RankingItem> Items);

public sealed record RankingItem(
    int Rank,
    Guid SessionTeamId,
    int VisibleScore,
    TimeSpan ResolutionTime);

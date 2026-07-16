namespace ScoringMonitoring.Application.Features.Rankings;

/// <summary>
/// The Ranking of a LiveSession, derived from its Scoreboard.
/// It summarises results and is not the source of truth: the Scoreboard is.
/// </summary>
/// <param name="LiveSessionId" example="c7a1d4e2-8b96-4f31-a0c5-2d7e6f8b1934">The LiveSession this Ranking was derived for.</param>
/// <param name="GeneratedAtUtc" example="2026-07-16T14:35:20.480Z">Server instant the projection was built. Because the Ranking is a derived read, this stamps how fresh the ordering is rather than when any score changed.</param>
/// <param name="Items">Teams ordered best-first. Empty until the Scoreboard has its first Score Entry. Ranks may repeat — see RankingItem.</param>
public sealed record RankingPayload(
    Guid LiveSessionId,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<RankingItem> Items);

/// <summary>
/// One Session Team's position within a Ranking.
/// </summary>
/// <param name="Rank" example="2">One-based position. A Shared Rank Tie repeats the same Rank across two or more consecutive items when they match on both VisibleScore and Resolution Time; no hidden third criterion breaks it, so consumers must not assume Rank is unique.</param>
/// <param name="SessionTeamId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">The ranked Session Team.</param>
/// <param name="VisibleScore" example="400">Accumulated score as shown to the business, floored at zero by the Score Floor even when penalties exceed the credits earned.</param>
/// <param name="ResolutionTime" example="00:12:47.5">Total Resolution Time of the team's credited Plays, used only as the tiebreaker between equal scores at 500 ms precision. It never contributes to VisibleScore.</param>
public sealed record RankingItem(
    int Rank,
    Guid SessionTeamId,
    int VisibleScore,
    TimeSpan ResolutionTime);


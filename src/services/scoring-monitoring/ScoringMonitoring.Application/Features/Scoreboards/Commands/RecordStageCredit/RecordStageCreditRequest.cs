namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;

/// <summary>
/// Reports a resolved Play so the Scoreboard can grant its Play Credit.
/// The LiveSession is taken from the route, not from this body.
/// </summary>
/// <param name="SessionTeamId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">The Session Team that resolved the Play, and the one the credit and Resolution Time accrue to.</param>
/// <param name="PlayId" example="9b2f1c7e-4d3a-4a51-8f0b-6c1d2e3f4a5b">The resolved Play. Together with SessionTeamId this is the Single-Play Credit key: a second call for the same pair grants no further positive credit.</param>
/// <param name="Difficulty" example="Medium">Play Difficulty, which fixes the Play Credit: Easy = 100, Medium = 200, Hard = 300. Same table for Trivia and Treasure Hunt, and No Partial Credit applies — the Play awards its full base score or nothing.</param>
/// <param name="ResolutionTime" example="00:04:12.5">Auditable time this Play took, added to the team's Resolution Time total. Used only to break Ranking ties at 500 ms precision; it never changes the score.</param>
/// <param name="RecordedAt" example="2026-07-16T14:32:05.000Z">Official instant of the resolution — backend receipt of the submission, not client clock. Stored on the Score Entry and on the Session Event Log entry.</param>
/// <param name="ValidationOverride">True when the credit comes from a manual Trivia correction by an Operator rather than automatic validation. Full Credit Override applies: the base score is identical, only the audit trail records the different channel.</param>
public sealed record RecordStageCreditRequest(
    Guid SessionTeamId,
    Guid PlayId,
    string Difficulty,
    TimeSpan ResolutionTime,
    DateTimeOffset RecordedAt,
    bool ValidationOverride);


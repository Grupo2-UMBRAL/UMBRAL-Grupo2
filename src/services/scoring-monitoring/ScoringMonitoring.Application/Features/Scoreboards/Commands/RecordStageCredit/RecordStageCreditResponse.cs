using ScoringMonitoring.Application.Features.Rankings;

namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;

/// <summary>
/// Outcome of a Play Credit attempt, with the Ranking as it stands afterwards.
/// </summary>
/// <param name="LiveSessionId" example="c7a1d4e2-8b96-4f31-a0c5-2d7e6f8b1934">The LiveSession whose Scoreboard absorbed the credit. The Scoreboard is created on the first credit of a session.</param>
/// <param name="SessionTeamId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">The Session Team the credit was evaluated for; VisibleScore below reports this team's total.</param>
/// <param name="PlayId" example="9b2f1c7e-4d3a-4a51-8f0b-6c1d2e3f4a5b">The Play that was reported as resolved.</param>
/// <param name="ScoreEntryId" example="5e8d3b21-9c47-4a6e-b8f2-1d0c7a4e9b63">The Score Entry that explains the score variation. Null when Single-Play Credit blocked the grant because this team had already been credited for this Play — nothing was written and no score changed.</param>
/// <param name="ScoreEntryCreated">False when the Play was already credited to this team, so the call was absorbed as a no-op rather than rejected.</param>
/// <param name="VisibleScore" example="400">The team's accumulated visible score after this call, subject to the Score Floor of zero.</param>
/// <param name="Ranking">The Ranking derived from the Scoreboard after this call, so callers need no follow-up read.</param>
public sealed record RecordStageCreditResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid PlayId,
    Guid? ScoreEntryId,
    bool ScoreEntryCreated,
    int VisibleScore,
    RankingPayload Ranking);


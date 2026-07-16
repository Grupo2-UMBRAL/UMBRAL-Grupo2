using ScoringMonitoring.Application.Features.Rankings;

namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.ApplyPenalty;

/// <summary>
/// Outcome of a Penalty application, with the Ranking as it stands afterwards.
/// </summary>
/// <param name="LiveSessionId" example="c7a1d4e2-8b96-4f31-a0c5-2d7e6f8b1934">The LiveSession whose Scoreboard absorbed the Penalty.</param>
/// <param name="SessionTeamId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">The sanctioned Session Team; VisibleScore below reports this team's total.</param>
/// <param name="CommandId" example="a4f0c9d8-1e72-4b35-96ca-8d5f2e7b0143">Echo of the idempotency key sent in the request, so a caller retrying blind can match this outcome to its own command.</param>
/// <param name="PenaltyId" example="6d2b8f04-3a75-4c19-9e83-b7f1c0a54d2e">The Penalty that was registered. Null when this CommandId had already been processed on this Scoreboard, so no second Penalty exists.</param>
/// <param name="ScoreEntryId" example="5e8d3b21-9c47-4a6e-b8f2-1d0c7a4e9b63">The Score Entry explaining the discount, which retains the full Penalty value even if the visible score saturated at zero. Null when the command was a duplicate and nothing was written.</param>
/// <param name="PenaltyApplied">False when the CommandId was already processed, meaning the retry was absorbed rather than rejected. It does not indicate an invalid request.</param>
/// <param name="VisibleScore" example="150">The team's accumulated visible score after the Penalty. Score Floor applies: it saturates at zero rather than going negative, while the Score Entry still records the whole discount.</param>
/// <param name="Ranking">The Ranking derived from the Scoreboard after this call, so callers need no follow-up read.</param>
public sealed record ApplyPenaltyResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    Guid? PenaltyId,
    Guid? ScoreEntryId,
    bool PenaltyApplied,
    int VisibleScore,
    RankingPayload Ranking);


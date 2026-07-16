using MediatR;
using SessionManagement.Application.Abstractions.Scoring;

namespace SessionManagement.Application.Features.Penalties;

public sealed record ApplyPenaltyCommand(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string Reason) : IRequest<ApplyPenaltyResponse>;

/// <summary>
/// A Penalty Application: the operator sanctions a Session Team by picking a predefined severity
/// and recording why. The point discount itself is resolved by Scoring and Monitoring, not here.
/// </summary>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Team to sanction. It must belong to the LiveSession in the route.</param>
/// <param name="CommandId" example="0a9b8c7d-6e5f-4a3b-9c2d-1e0f9a8b7c6d">Caller-generated id that makes the command idempotent: retrying with the same id will not sanction twice. Use a fresh id for a genuinely different sanction.</param>
/// <param name="Severity" example="Minor">Minor, Major or Critical. The operator picks a severity rather than a number; each maps to a fixed discount.</param>
/// <param name="Reason" example="Team split up against the rules">Justification stored for audit. Required.</param>
public sealed record ApplyPenaltyRequest(
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string Reason);

/// <summary>
/// Result of a Penalty Application, echoing the scoring side's decision and the refreshed ranking.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session the sanction was applied in.</param>
/// <param name="SessionTeamId" example="6f2e5d84-1a7b-4c39-9e02-5b8d3f7a1c46">Sanctioned team.</param>
/// <param name="CommandId" example="0a9b8c7d-6e5f-4a3b-9c2d-1e0f9a8b7c6d">Idempotency id echoed from the request, so callers can match a retry to its original command.</param>
/// <param name="PenaltyId" example="3d7c9e15-5b82-4f60-a934-6c1d8b2e7f05">Penalty recorded by Scoring and Monitoring. null when nothing new was recorded because the command was a duplicate.</param>
/// <param name="ScoreEntryId" example="9a0b1c2d-3e4f-4a5b-8c6d-7e8f9a0b1c2d">Score Entry holding the discount. null when the command was a duplicate and no new entry was written.</param>
/// <param name="PenaltyApplied">false = the command was recognised as a repeat of an earlier one and deliberately ignored; the response still reports the current score.</param>
/// <param name="VisibleScore" example="120">The team's score after this call, as shown to participants.</param>
/// <param name="Ranking">Ranking recomputed after the sanction, so operators need no second call to see the effect.</param>
/// <param name="RecordedAtUtc" example="2026-07-16T21:25:00Z">Instant Session Operations recorded the sanction.</param>
public sealed record ApplyPenaltyResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    Guid? PenaltyId,
    Guid? ScoreEntryId,
    bool PenaltyApplied,
    int VisibleScore,
    RankingPayload Ranking,
    DateTimeOffset RecordedAtUtc);


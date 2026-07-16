namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.ApplyPenalty;

/// <summary>
/// Applies an explicit Penalty decided by an Operator to a Session Team.
/// Explicit Penalty Only: invalid attempts and rejected evidence never discount score on their own,
/// so this request is the only way the Scoreboard loses points. The LiveSession comes from the route.
/// </summary>
/// <param name="SessionTeamId" example="3fa85f64-5717-4562-b3fc-2c963f66afa6">The Session Team being sanctioned.</param>
/// <param name="CommandId" example="a4f0c9d8-1e72-4b35-96ca-8d5f2e7b0143">Caller-generated idempotency key. Resending the same CommandId is absorbed without discounting twice. Distinct Penalty Events: a genuinely separate sanction against the same team needs a new CommandId, otherwise it will be silently ignored as a duplicate.</param>
/// <param name="Severity" example="Major">Penalty Severity, which fixes the discount from a closed table: Minor = -50, Major = -100, Critical = -200. Free amounts and percentages are not supported.</param>
/// <param name="AppliedByOperatorUserId" example="8c1e4a90-7f3b-4d26-b5a8-0e9c3f2d7614">The Operator accountable for the decision, kept for audit and echoed into the Session Event Log description.</param>
/// <param name="Reason" example="Team used an external map outside the mission area">Explicit motive for the sanction. Copied verbatim into the Session Event Log, so it is what a supervisor reads later to justify the discount.</param>
/// <param name="RecordedAt" example="2026-07-16T14:35:20.000Z">Official instant the Penalty took effect, stored on the resulting Score Entry and Session Event Log entry.</param>
public sealed record ApplyPenaltyRequest(
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string AppliedByOperatorUserId,
    string Reason,
    DateTimeOffset RecordedAt);


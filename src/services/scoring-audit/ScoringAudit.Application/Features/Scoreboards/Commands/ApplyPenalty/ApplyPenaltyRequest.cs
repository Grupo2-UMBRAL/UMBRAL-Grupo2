namespace ScoringAudit.Application.Features.Scoreboards.Commands.ApplyPenalty;

public sealed record ApplyPenaltyRequest(
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string AppliedByOperatorUserId,
    string Reason,
    DateTimeOffset RecordedAt);

namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.ApplyPenalty;

public sealed record ApplyPenaltyCommand(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    string Severity,
    string AppliedByOperatorUserId,
    string Reason,
    DateTimeOffset RecordedAt) : IRequest<ApplyPenaltyResponse>;

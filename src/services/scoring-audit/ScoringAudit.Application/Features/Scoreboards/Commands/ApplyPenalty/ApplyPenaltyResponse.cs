using ScoringAudit.Application.Features.Rankings;

namespace ScoringAudit.Application.Features.Scoreboards.Commands.ApplyPenalty;

public sealed record ApplyPenaltyResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid CommandId,
    Guid? PenaltyId,
    Guid? ScoreEntryId,
    bool PenaltyApplied,
    int VisibleScore,
    RankingPayload Ranking);

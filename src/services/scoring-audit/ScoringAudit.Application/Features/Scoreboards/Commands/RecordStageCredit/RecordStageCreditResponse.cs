using ScoringAudit.Application.Features.Rankings;

namespace ScoringAudit.Application.Features.Scoreboards.Commands.RecordStageCredit;

public sealed record RecordStageCreditResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    Guid? ScoreEntryId,
    bool ScoreEntryCreated,
    int VisibleScore,
    RankingPayload Ranking);

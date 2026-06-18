using ScoringMonitoring.Application.Features.Rankings;

namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;

public sealed record RecordStageCreditResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    Guid? ScoreEntryId,
    bool ScoreEntryCreated,
    int VisibleScore,
    RankingPayload Ranking);

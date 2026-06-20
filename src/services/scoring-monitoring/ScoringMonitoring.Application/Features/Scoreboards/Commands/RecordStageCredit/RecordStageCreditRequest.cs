namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;

public sealed record RecordStageCreditRequest(
    Guid SessionTeamId,
    Guid MissionStageId,
    string Difficulty,
    TimeSpan ResolutionTime,
    DateTimeOffset RecordedAt,
    bool ValidationOverride);


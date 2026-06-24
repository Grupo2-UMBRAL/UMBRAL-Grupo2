namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;

public sealed record RecordStageCreditRequest(
    Guid SessionTeamId,
    Guid PlayId,
    string Difficulty,
    TimeSpan ResolutionTime,
    DateTimeOffset RecordedAt,
    bool ValidationOverride);


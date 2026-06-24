namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;

public sealed record RecordStageCreditCommand(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid PlayId,
    string Difficulty,
    TimeSpan ResolutionTime,
    DateTimeOffset RecordedAt,
    bool ValidationOverride) : IRequest<RecordStageCreditResponse>;


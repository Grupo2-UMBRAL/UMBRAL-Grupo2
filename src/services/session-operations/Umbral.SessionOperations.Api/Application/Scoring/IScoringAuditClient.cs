namespace Umbral.SessionOperations.Api.Application.Scoring;

public interface IScoringAuditClient
{
    Task RecordStageCreditAsync(
        RecordStageCreditRequest request,
        CancellationToken cancellationToken);
}

public sealed record RecordStageCreditRequest(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    string Difficulty,
    TimeSpan ResolutionTime,
    DateTimeOffset RecordedAt,
    bool ValidationOverride);

using Microsoft.EntityFrameworkCore;
using ScoringAudit.Application.Features.Audit;
using ScoringAudit.Application.Features.Rankings;
using ScoringAudit.Domain.Audit;
using ScoringAudit.Domain.Scoreboards;

namespace ScoringAudit.Application.Features.Scoreboards.Commands.RecordStageCredit;

public sealed class RecordStageCreditHandler(
    IScoringAuditDbContext dbContext,
    TimeProvider timeProvider,
    IScoringAuditUpdatesPublisher updatesPublisher)
    : IRequestHandler<RecordStageCreditCommand, RecordStageCreditResponse>
{
    public async Task<RecordStageCreditResponse> Handle(
        RecordStageCreditCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scoreboard = await dbContext.Scoreboards
            .Include(entity => entity.ScoreEntries)
            .SingleOrDefaultAsync(entity => entity.LiveSessionId == request.LiveSessionId, cancellationToken);
        if (scoreboard is null)
        {
            scoreboard = new Scoreboard(request.LiveSessionId);
            dbContext.Scoreboards.Add(scoreboard);
        }
        else
        {
            scoreboard.RebuildState();
        }

        var scoreEntry = scoreboard.GrantStageCredit(
            request.SessionTeamId,
            request.MissionStageId,
            ParseDifficulty(request.Difficulty),
            request.ResolutionTime,
            request.RecordedAt,
            request.ValidationOverride);

        SessionEventLog? eventLog = null;
        if (scoreEntry is not null)
        {
            eventLog = new SessionEventLog(
                Guid.NewGuid(),
                request.LiveSessionId,
                "StageCredit",
                CreateStageCreditDescription(request, scoreEntry),
                request.RecordedAt);
            dbContext.SessionEventLogs.Add(eventLog);
            await dbContext.SaveChangesAsync(cancellationToken);
            scoreboard.RebuildState();
        }

        var ranking = RankingProjection.Create(scoreboard, timeProvider.GetUtcNow());
        if (scoreEntry is not null)
        {
            await updatesPublisher.PublishRankingUpdatedAsync(ranking, cancellationToken);

            if (eventLog is not null)
            {
                await updatesPublisher.PublishEventLogUpdatedAsync(
                    SessionEventLogPayload.FromEntity(eventLog),
                    cancellationToken);
            }
        }

        return new RecordStageCreditResponse(
            scoreboard.LiveSessionId,
            request.SessionTeamId,
            request.MissionStageId,
            scoreEntry?.ScoreEntryId,
            scoreEntry is not null,
            scoreboard.GetTeamScore(request.SessionTeamId).VisibleScore,
            ranking);
    }

    private static MissionStageDifficulty ParseDifficulty(string difficulty)
    {
        if (Enum.TryParse<MissionStageDifficulty>(difficulty, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new UmbralDomainException(
            "scoreboard.unknown_difficulty",
            "Mission Stage difficulty is not supported.",
            UmbralFailureCategory.Validation);
    }

    private static string CreateStageCreditDescription(
        RecordStageCreditCommand request,
        ScoreEntry scoreEntry)
    {
        var source = request.ValidationOverride ? " through Validation Override" : string.Empty;

        return $"Session Team '{request.SessionTeamId}' completed Mission Stage '{request.MissionStageId}'{source} and received {scoreEntry.Delta} point(s). Visible score: {scoreEntry.VisibleScoreAfter}.";
    }
}

using ScoringMonitoring.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Application.Features.SessionEventLogs;
using ScoringMonitoring.Application.Features.Rankings;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Domain.Scoreboards;

namespace ScoringMonitoring.Application.Features.Scoreboards.Commands.RecordStageCredit;

public sealed class RecordStageCreditHandler(
    IUnitOfWork unitOfWork, IRepository<Scoreboard> scoreboardRepository, IRepository<SessionEventLog> sessionEventLogRepository,
    TimeProvider timeProvider,
    IScoringMonitoringUpdatesPublisher updatesPublisher)
    : IRequestHandler<RecordStageCreditCommand, RecordStageCreditResponse>
{
    public async Task<RecordStageCreditResponse> Handle(
        RecordStageCreditCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scoreboard = await scoreboardRepository
            .Include(entity => entity.ScoreEntries)
            .SingleOrDefaultAsync(entity => entity.LiveSessionId == request.LiveSessionId, cancellationToken);
        if (scoreboard is null)
        {
            scoreboard = new Scoreboard(request.LiveSessionId);
            scoreboardRepository.Add(scoreboard);
        }
        else
        {
            scoreboard.RebuildState();
        }

        var scoreEntry = scoreboard.GrantPlayCredit(
            request.SessionTeamId,
            request.PlayId,
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
            sessionEventLogRepository.Add(eventLog);
            await unitOfWork.SaveChangesAsync(cancellationToken);
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
            request.PlayId,
            scoreEntry?.ScoreEntryId,
            scoreEntry is not null,
            scoreboard.GetTeamScore(request.SessionTeamId).VisibleScore,
            ranking);
    }

    private static PlayDifficulty ParseDifficulty(string difficulty)
    {
        if (Enum.TryParse<PlayDifficulty>(difficulty, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new UmbralDomainException(
            "scoreboard.unknown_difficulty",
            "Play difficulty is not supported.",
            UmbralFailureCategory.Validation);
    }

    private static string CreateStageCreditDescription(
        RecordStageCreditCommand request,
        ScoreEntry scoreEntry)
    {
        var source = request.ValidationOverride ? " through Validation Override" : string.Empty;

        return $"Session Team '{request.SessionTeamId}' completed Play '{request.PlayId}'{source} and received {scoreEntry.Delta} point(s). Visible score: {scoreEntry.VisibleScoreAfter}.";
    }
}



using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ScoringAudit.Api.Application.Audit;
using Umbral.ScoringAudit.Api.Application.Rankings;
using Umbral.ScoringAudit.Api.Domain.Audit;
using Umbral.ScoringAudit.Api.Domain.Scoreboards;
using Umbral.ScoringAudit.Api.Hubs;
using Umbral.ScoringAudit.Api.Hubs.Contracts;
using Umbral.ScoringAudit.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Application.Scoreboards;

public sealed record RecordStageCreditCommand(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    string Difficulty,
    TimeSpan ResolutionTime,
    DateTimeOffset RecordedAt,
    bool ValidationOverride) : IRequest<RecordStageCreditResponse>;

public sealed record RecordStageCreditRequest(
    Guid SessionTeamId,
    Guid MissionStageId,
    string Difficulty,
    TimeSpan ResolutionTime,
    DateTimeOffset RecordedAt,
    bool ValidationOverride);

public sealed record RecordStageCreditResponse(
    Guid LiveSessionId,
    Guid SessionTeamId,
    Guid MissionStageId,
    Guid? ScoreEntryId,
    bool ScoreEntryCreated,
    int VisibleScore,
    RankingPayload Ranking);

public sealed class RecordStageCreditHandler(
    ScoringAuditDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<ScoringAuditHub, IScoringAuditClient> hubContext)
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
            await hubContext.Clients.All.ReceiveRankingUpdated(ranking).WaitAsync(cancellationToken);

            if (eventLog is not null)
            {
                await hubContext.Clients.All
                    .ReceiveEventLogUpdated(SessionEventLogPayload.FromEntity(eventLog))
                    .WaitAsync(cancellationToken);
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

using ScoringMonitoring.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Domain.Scoreboards;

namespace ScoringMonitoring.Application.Features.Rankings.Queries.GetRanking;

public sealed class GetRankingHandler(
    IScoreboardRepository scoreboardRepository,
    TimeProvider timeProvider)
    : IRequestHandler<GetRankingQuery, RankingPayload>
{
    public async Task<RankingPayload> Handle(
        GetRankingQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scoreboard = await scoreboardRepository.GetByLiveSessionIdAsync(request.LiveSessionId, cancellationToken);
        if (scoreboard is null)
        {
            throw new UmbralDomainException(
                "scoreboard_not_found",
                $"Scoreboard for LiveSession '{request.LiveSessionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        scoreboard.RebuildState();
        return RankingProjection.Create(scoreboard, timeProvider.GetUtcNow());
    }
}



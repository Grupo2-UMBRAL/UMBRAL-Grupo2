using Microsoft.EntityFrameworkCore;
using ScoringAudit.Domain.Scoreboards;

namespace ScoringAudit.Application.Features.Rankings.Queries.GetRanking;

public sealed class GetRankingHandler(
    IScoringAuditDbContext dbContext,
    TimeProvider timeProvider)
    : IRequestHandler<GetRankingQuery, RankingPayload>
{
    public async Task<RankingPayload> Handle(
        GetRankingQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scoreboard = await dbContext.Scoreboards
            .Include(entity => entity.ScoreEntries)
            .SingleOrDefaultAsync(entity => entity.LiveSessionId == request.LiveSessionId, cancellationToken);
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

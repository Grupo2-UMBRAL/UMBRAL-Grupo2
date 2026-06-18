using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Application.Features.Scoreboards;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Domain.Scoreboards;

namespace ScoringMonitoring.Infrastructure.Persistence;

public sealed class ApplyPenaltyScoreboardStore(ScoringMonitoringDbContext dbContext) : IApplyPenaltyScoreboardStore
{
    public async Task<Scoreboard> LoadAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var scoreboard = await dbContext.Scoreboards
            .Include(entity => entity.ScoreEntries)
            .SingleOrDefaultAsync(entity => entity.LiveSessionId == liveSessionId, cancellationToken);

        if (scoreboard is null)
        {
            scoreboard = new Scoreboard(liveSessionId);
            dbContext.Scoreboards.Add(scoreboard);

            return scoreboard;
        }

        scoreboard.RebuildState();
        return scoreboard;
    }

    public async Task PersistPenaltyApplicationAsync(
        SessionEventLog eventLog,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventLog);

        dbContext.SessionEventLogs.Add(eventLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

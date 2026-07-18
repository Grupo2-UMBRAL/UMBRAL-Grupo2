using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Application.Abstractions;
using ScoringMonitoring.Domain.Scoreboards;

namespace ScoringMonitoring.Infrastructure.Persistence;

internal sealed class ScoreboardRepository(ScoringMonitoringDbContext dbContext) : IScoreboardRepository
{
    public async Task<Scoreboard?> GetByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var scoreboard = await dbContext.Scoreboards
            .Include(entity => entity.ScoreEntries)
            .SingleOrDefaultAsync(entity => entity.LiveSessionId == liveSessionId, cancellationToken);

        if (scoreboard is not null)
        {
            scoreboard.RebuildState();
        }

        return scoreboard;
    }

    public async Task<Scoreboard> GetRequiredByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        var scoreboard = await GetByLiveSessionIdAsync(liveSessionId, cancellationToken);
        if (scoreboard is null)
        {
            scoreboard = new Scoreboard(liveSessionId);
            dbContext.Scoreboards.Add(scoreboard);
        }

        return scoreboard;
    }

    public void Add(Scoreboard scoreboard)
    {
        ArgumentNullException.ThrowIfNull(scoreboard);
        dbContext.Scoreboards.Add(scoreboard);
    }
}

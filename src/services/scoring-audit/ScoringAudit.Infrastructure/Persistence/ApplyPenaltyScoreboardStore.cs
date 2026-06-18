using Microsoft.EntityFrameworkCore;
using ScoringAudit.Application.Features.Scoreboards;
using ScoringAudit.Domain.Audit;
using ScoringAudit.Domain.Scoreboards;

namespace ScoringAudit.Infrastructure.Persistence;

public sealed class ApplyPenaltyScoreboardStore(ScoringAuditDbContext dbContext) : IApplyPenaltyScoreboardStore
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

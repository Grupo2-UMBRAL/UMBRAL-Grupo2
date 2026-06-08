using Microsoft.EntityFrameworkCore;
using Umbral.ScoringAudit.Api.Application.Scoreboards;
using Umbral.ScoringAudit.Api.Domain.Audit;
using Umbral.ScoringAudit.Api.Domain.Scoreboards;

namespace Umbral.ScoringAudit.Api.Infrastructure;

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

using Microsoft.EntityFrameworkCore;
using Umbral.ScoringAudit.Api.Domain.Audit;
using Umbral.ScoringAudit.Api.Domain.Scoreboards;

namespace Umbral.ScoringAudit.Api.Application;

public interface IScoringAuditDbContext
{
    DbSet<Scoreboard> Scoreboards { get; }

    DbSet<ScoreEntry> ScoreEntries { get; }

    DbSet<SessionEventLog> SessionEventLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

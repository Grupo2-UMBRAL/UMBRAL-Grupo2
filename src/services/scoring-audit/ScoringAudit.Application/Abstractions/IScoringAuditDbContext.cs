using Microsoft.EntityFrameworkCore;
using ScoringAudit.Domain.Audit;
using ScoringAudit.Domain.Scoreboards;

namespace ScoringAudit.Application.Abstractions;

public interface IScoringAuditDbContext
{
    DbSet<Scoreboard> Scoreboards { get; }

    DbSet<ScoreEntry> ScoreEntries { get; }

    DbSet<SessionEventLog> SessionEventLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

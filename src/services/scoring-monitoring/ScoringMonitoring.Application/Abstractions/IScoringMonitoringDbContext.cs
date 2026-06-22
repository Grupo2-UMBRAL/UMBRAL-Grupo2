using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Domain.Scoreboards;

namespace ScoringMonitoring.Application.Abstractions;

public interface IScoringMonitoringDbContext
{
    DbSet<Scoreboard> Scoreboards { get; }

    DbSet<ScoreEntry> ScoreEntries { get; }

    DbSet<SessionEventLog> SessionEventLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

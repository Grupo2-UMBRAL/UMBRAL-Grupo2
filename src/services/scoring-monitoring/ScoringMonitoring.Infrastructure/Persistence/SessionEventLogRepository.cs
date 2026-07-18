using Microsoft.EntityFrameworkCore;
using ScoringMonitoring.Application.Abstractions;
using ScoringMonitoring.Domain.Audit;

namespace ScoringMonitoring.Infrastructure.Persistence;

internal sealed class SessionEventLogRepository(ScoringMonitoringDbContext dbContext) : ISessionEventLogRepository
{
    public async Task<SessionEventLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await dbContext.SessionEventLogs
            .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SessionEventLog>> GetByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken)
    {
        return await dbContext.SessionEventLogs
            .Where(entity => entity.LiveSessionId == liveSessionId)
            .ToListAsync(cancellationToken);
    }

    public void Add(SessionEventLog log)
    {
        ArgumentNullException.ThrowIfNull(log);
        dbContext.SessionEventLogs.Add(log);
    }
}

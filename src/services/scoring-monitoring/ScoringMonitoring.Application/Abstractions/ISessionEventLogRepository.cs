using ScoringMonitoring.Domain.Audit;

namespace ScoringMonitoring.Application.Abstractions;

public interface ISessionEventLogRepository
{
    Task<SessionEventLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionEventLog>> GetByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken);
    void Add(SessionEventLog log);
}

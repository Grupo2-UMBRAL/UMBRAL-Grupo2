using ScoringMonitoring.Domain.Scoreboards;

namespace ScoringMonitoring.Application.Abstractions;

public interface IScoreboardRepository
{
    Task<Scoreboard?> GetByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken);
    Task<Scoreboard> GetRequiredByLiveSessionIdAsync(Guid liveSessionId, CancellationToken cancellationToken);
    void Add(Scoreboard scoreboard);
}

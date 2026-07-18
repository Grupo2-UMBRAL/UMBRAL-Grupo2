using ScoringMonitoring.Application.Features.SessionEventLogs;
using ScoringMonitoring.Application.Features.Rankings;
using ScoringMonitoring.Domain.Audit;
using ScoringMonitoring.Domain.Scoreboards;

namespace ScoringMonitoring.Application.Features.Scoreboards;



public interface IScoringMonitoringUpdatesPublisher
{
    Task PublishRankingUpdatedAsync(RankingPayload ranking, CancellationToken cancellationToken);

    Task PublishEventLogUpdatedAsync(SessionEventLogPayload eventLog, CancellationToken cancellationToken);
}


using ScoringMonitoring.Application.Features.SessionEventLogs;
using ScoringMonitoring.Application.Features.Rankings;

namespace ScoringMonitoring.Application.Hubs;

public interface IScoringMonitoringClient
{
    Task ReceiveRankingUpdated(RankingPayload payload);

    Task ReceiveEventLogUpdated(SessionEventLogPayload payload);
}

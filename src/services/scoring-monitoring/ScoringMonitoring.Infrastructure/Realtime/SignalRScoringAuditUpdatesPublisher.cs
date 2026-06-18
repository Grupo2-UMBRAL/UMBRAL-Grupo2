using Microsoft.AspNetCore.SignalR;
using ScoringMonitoring.Application.Hubs;

namespace ScoringMonitoring.Infrastructure.Realtime;

public sealed class SignalRScoringMonitoringUpdatesPublisher(
    IHubContext<ScoringMonitoringHub, IScoringMonitoringClient> hubContext)
    : IScoringMonitoringUpdatesPublisher
{
    public async Task PublishRankingUpdatedAsync(
        RankingPayload ranking,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ranking);

        await hubContext.Clients.All.ReceiveRankingUpdated(ranking).WaitAsync(cancellationToken);
    }

    public async Task PublishEventLogUpdatedAsync(
        SessionEventLogPayload eventLog,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventLog);

        await hubContext.Clients.All.ReceiveEventLogUpdated(eventLog).WaitAsync(cancellationToken);
    }
}

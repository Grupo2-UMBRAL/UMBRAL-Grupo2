using Microsoft.AspNetCore.SignalR;
using ScoringAudit.Application.Hubs;

namespace ScoringAudit.Infrastructure.Realtime;

public sealed class SignalRScoringAuditUpdatesPublisher(
    IHubContext<ScoringAuditHub, IScoringAuditClient> hubContext)
    : IScoringAuditUpdatesPublisher
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

using Microsoft.AspNetCore.SignalR;
using Umbral.ScoringAudit.Api.Application.Audit;
using Umbral.ScoringAudit.Api.Application.Rankings;
using Umbral.ScoringAudit.Api.Application.Scoreboards;
using Umbral.ScoringAudit.Api.Hubs;
using Umbral.ScoringAudit.Api.Hubs.Contracts;

namespace Umbral.ScoringAudit.Api.Presentation.Realtime;

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

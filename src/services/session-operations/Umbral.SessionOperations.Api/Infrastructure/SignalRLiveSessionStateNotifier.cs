using Microsoft.AspNetCore.SignalR;
using Umbral.SessionOperations.Api.Application.SessionLifecycle;
using Umbral.SessionOperations.Api.Application.Realtime;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Hubs.Contracts;

namespace Umbral.SessionOperations.Api.Infrastructure;

public sealed class SignalRLiveSessionRealtimeNotifier(IHubContext<SessionOperationsHub, ISessionClient> hubContext)
    : ISessionRealtimeNotifier
{
    public Task NotifySessionStateChangedAsync(
        LiveSessionStateChangedEvent stateChangedEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stateChangedEvent);

        var payload = new SessionStateChangedPayload(
            new RealtimeEventMetadata(
                stateChangedEvent.LiveSessionId,
                stateChangedEvent.SequenceNumber,
                stateChangedEvent.OccurredAtUtc,
                SnapshotRefreshPolicy.RefreshSnapshot,
                stateChangedEvent.Reason),
            stateChangedEvent.PreviousState,
            stateChangedEvent.State,
            null);

        return hubContext.Clients.All.ReceiveSessionStateChanged(payload).WaitAsync(cancellationToken);
    }

    public Task NotifyTeamProgressChangedAsync(
        TeamProgressChangedPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return hubContext.Clients.All.ReceiveTeamProgressChanged(payload).WaitAsync(cancellationToken);
    }

    public Task NotifyEvidenceSubmissionOutcomeChangedAsync(
        EvidenceSubmissionOutcomeChangedPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return hubContext.Clients.All.ReceiveEvidenceSubmissionOutcomeChanged(payload).WaitAsync(cancellationToken);
    }

    public Task NotifyHintUnlockedAsync(
        HintUnlockedPayload payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return hubContext.Clients.All.ReceiveHintUnlocked(payload).WaitAsync(cancellationToken);
    }
}

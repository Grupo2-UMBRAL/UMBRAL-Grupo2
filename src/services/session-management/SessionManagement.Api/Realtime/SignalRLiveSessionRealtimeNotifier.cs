using Microsoft.AspNetCore.SignalR;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.SessionLifecycle;

namespace SessionManagement.Api.Realtime;

/// <summary>
/// SignalR-backed implementation of <see cref="ISessionRealtimeNotifier"/>. Lives in the API layer
/// alongside the hub it drives so the Application layer stays free of any web/transport framework.
/// </summary>
public sealed class SignalRLiveSessionRealtimeNotifier(IHubContext<SessionManagementHub, ISessionClient> hubContext)
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

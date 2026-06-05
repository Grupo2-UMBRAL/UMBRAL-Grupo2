using Microsoft.AspNetCore.SignalR;
using Umbral.SessionOperations.Api.Application.SessionLifecycle;
using Umbral.SessionOperations.Api.Hubs;

namespace Umbral.SessionOperations.Api.Infrastructure;

public static class SessionOperationsHubEvents
{
    public const string LiveSessionStateChanged = "liveSessionStateChanged";
}

public sealed class SignalRLiveSessionStateNotifier(IHubContext<SessionOperationsHub> hubContext)
    : ILiveSessionStateNotifier
{
    public Task NotifyStateChangedAsync(
        LiveSessionStateChangedEvent stateChangedEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stateChangedEvent);

        return hubContext.Clients.All.SendAsync(
            SessionOperationsHubEvents.LiveSessionStateChanged,
            stateChangedEvent,
            cancellationToken);
    }
}

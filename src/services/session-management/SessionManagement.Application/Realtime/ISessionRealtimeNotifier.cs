using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Hubs.Contracts;

namespace SessionManagement.Application.Realtime;

public interface ISessionRealtimeNotifier
{
    Task NotifySessionStateChangedAsync(
        LiveSessionStateChangedEvent stateChangedEvent,
        CancellationToken cancellationToken);

    Task NotifyTeamProgressChangedAsync(
        TeamProgressChangedPayload payload,
        CancellationToken cancellationToken);

    Task NotifyEvidenceSubmissionOutcomeChangedAsync(
        EvidenceSubmissionOutcomeChangedPayload payload,
        CancellationToken cancellationToken);

    Task NotifyHintUnlockedAsync(
        HintUnlockedPayload payload,
        CancellationToken cancellationToken);
}

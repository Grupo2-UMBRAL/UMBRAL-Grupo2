using SessionManagement.Application.Features.SessionLifecycle;

namespace SessionManagement.Application.Abstractions.Realtime;

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

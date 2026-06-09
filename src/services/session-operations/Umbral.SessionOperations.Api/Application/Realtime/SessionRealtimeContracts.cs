using Umbral.SessionOperations.Api.Application.SessionLifecycle;
using Umbral.SessionOperations.Api.Hubs.Contracts;

namespace Umbral.SessionOperations.Api.Application.Realtime;

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

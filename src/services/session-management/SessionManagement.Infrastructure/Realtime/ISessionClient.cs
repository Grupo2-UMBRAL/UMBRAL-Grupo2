using SessionManagement.Application.Abstractions.Realtime;

namespace SessionManagement.Infrastructure.Realtime;

/// <summary>
/// Strongly-typed SignalR client contract for the session hub. Lives in Infrastructure alongside
/// the hub and notifier that use it; the Application layer only knows the abstract
/// <see cref="ISessionRealtimeNotifier"/> and the payload records it carries.
/// </summary>
public interface ISessionClient
{
    Task ReceiveSessionStateChanged(SessionStateChangedPayload payload);

    Task ReceiveTeamProgressChanged(TeamProgressChangedPayload payload);

    Task ReceiveEvidenceSubmissionOutcomeChanged(EvidenceSubmissionOutcomeChangedPayload payload);

    Task ReceiveHintUnlocked(HintUnlockedPayload payload);
}

using SessionManagement.Application.Abstractions.Realtime;

namespace SessionManagement.Api.Realtime;

/// <summary>
/// Strongly-typed SignalR client contract for the session hub. Lives in the API/presentation
/// layer because it is a transport-facing detail; the Application layer only knows the abstract
/// <see cref="ISessionRealtimeNotifier"/> and the payload records it carries.
/// </summary>
public interface ISessionClient
{
    Task ReceiveSessionStateChanged(SessionStateChangedPayload payload);

    Task ReceiveTeamProgressChanged(TeamProgressChangedPayload payload);

    Task ReceiveEvidenceSubmissionOutcomeChanged(EvidenceSubmissionOutcomeChangedPayload payload);

    Task ReceiveHintUnlocked(HintUnlockedPayload payload);
}

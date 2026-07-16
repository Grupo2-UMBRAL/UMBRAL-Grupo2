namespace SessionManagement.Application.Features.SessionLifecycle;

/// <summary>
/// State of a LiveSession after a Session Lifecycle transition (start, pause, resume, finalize or
/// cancel). Reports the global state only; per-team progress is not affected by these transitions.
/// </summary>
/// <param name="LiveSessionId" example="5e1d9f34-8b26-4a7c-9f10-3c2b5d6e7a80">Session that was transitioned.</param>
/// <param name="State" example="Active">Session State reached: Scheduled, Active, Paused, Finalized or Canceled. Determines which advances, evidence and operator actions the session now admits.</param>
/// <param name="RegisteredSessionTeamCount" example="6">Session Teams registered when the transition happened. Useful right after a start, which also closes the Team Assignment Window.</param>
/// <param name="EnrollmentWindowOpenedAtUtc" example="2026-07-16T20:45:00Z">Instant the Team Assignment Window opened. null = it was never opened.</param>
/// <param name="EnrollmentWindowClosedAtUtc" example="2026-07-16T21:00:00Z">Instant the Team Assignment Window closed. null = it was never closed, so participants can still change teams.</param>
public sealed record LiveSessionStateResponse(
    Guid LiveSessionId,
    string State,
    int RegisteredSessionTeamCount,
    DateTimeOffset? EnrollmentWindowOpenedAtUtc,
    DateTimeOffset? EnrollmentWindowClosedAtUtc);

public sealed record LiveSessionStateChangedEvent(
    Guid LiveSessionId,
    string PreviousState,
    string State,
    int RegisteredSessionTeamCount,
    long SequenceNumber,
    string Reason,
    DateTimeOffset OccurredAtUtc);

public interface ILiveSessionStateNotifier
{
    Task NotifyStateChangedAsync(
        LiveSessionStateChangedEvent stateChangedEvent,
        CancellationToken cancellationToken);
}


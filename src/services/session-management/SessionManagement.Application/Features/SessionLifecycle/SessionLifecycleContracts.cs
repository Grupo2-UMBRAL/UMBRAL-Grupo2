namespace SessionManagement.Application.Features.SessionLifecycle;

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


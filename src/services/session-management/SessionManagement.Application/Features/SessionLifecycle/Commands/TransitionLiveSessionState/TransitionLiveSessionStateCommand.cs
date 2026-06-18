using MediatR;

namespace SessionManagement.Application.Features.SessionLifecycle;

public enum LiveSessionLifecycleAction
{
    Start,
    Pause,
    Resume,
    Finalize,
    Cancel
}

public sealed record TransitionLiveSessionStateCommand(
    Guid LiveSessionId,
    LiveSessionLifecycleAction Action) : IRequest<LiveSessionStateResponse>
{
}

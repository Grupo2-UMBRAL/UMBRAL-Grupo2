using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.LiveSessions;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.SessionLifecycle;

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
    LiveSessionLifecycleAction Action) : IRequest<LiveSessionStateResponse>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOrOperator;
}

public sealed class TransitionLiveSessionStateCommandHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider,
    ILiveSessionStateNotifier stateNotifier)
    : IRequestHandler<TransitionLiveSessionStateCommand, LiveSessionStateResponse>
{
    public async Task<LiveSessionStateResponse> Handle(
        TransitionLiveSessionStateCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
            .Include(existingLiveSession => existingLiveSession.SessionTeams)
            .SingleOrDefaultAsync(
                existingLiveSession => existingLiveSession.Id == request.LiveSessionId,
                cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                $"LiveSession '{request.LiveSessionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        var previousState = liveSession.State;
        var occurredAtUtc = timeProvider.GetUtcNow();

        ApplyTransition(liveSession, request.Action, occurredAtUtc);

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = liveSession.ToStateResponse();

        await stateNotifier.NotifyStateChangedAsync(
            new LiveSessionStateChangedEvent(
                liveSession.Id,
                previousState,
                liveSession.State,
                liveSession.SessionTeams.Count,
                occurredAtUtc),
            cancellationToken);

        return response;
    }

    private static void ApplyTransition(
        LiveSession liveSession,
        LiveSessionLifecycleAction action,
        DateTimeOffset occurredAtUtc)
    {
        switch (action)
        {
            case LiveSessionLifecycleAction.Start:
                liveSession.Start(occurredAtUtc);
                return;
            case LiveSessionLifecycleAction.Pause:
                liveSession.Pause();
                return;
            case LiveSessionLifecycleAction.Resume:
                liveSession.Resume();
                return;
            case LiveSessionLifecycleAction.Finalize:
                liveSession.FinalizeSession();
                return;
            case LiveSessionLifecycleAction.Cancel:
                liveSession.Cancel();
                return;
            default:
                throw new UmbralDomainException(
                    "live_session_lifecycle_action_invalid",
                    $"Unsupported lifecycle action '{action}'.",
                    UmbralFailureCategory.Validation);
        }
    }
}

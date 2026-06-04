using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.LiveSessions;
using Umbral.SessionOperations.Api.Application.SessionSnapshots;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Hubs.Contracts;
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
    ILiveSessionStateNotifier stateNotifier,
    IHubContext<SessionOperationsHub, ISessionClient> hubContext)
    : IRequestHandler<TransitionLiveSessionStateCommand, LiveSessionStateResponse>
{
    public async Task<LiveSessionStateResponse> Handle(
        TransitionLiveSessionStateCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
            .Include(existingLiveSession => existingLiveSession.SessionTeams)
            .Include(existingLiveSession => existingLiveSession.ReleasedHints)
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

        var newlyReleasedHints = ApplyTransition(liveSession, request.Action, occurredAtUtc);

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

        foreach (var releasedHint in newlyReleasedHints)
        {
            await PublishHintUnlockedAsync(liveSession, releasedHint, occurredAtUtc, cancellationToken);
        }

        return response;
    }

    private static IReadOnlyList<ReleasedHint> ApplyTransition(
        LiveSession liveSession,
        LiveSessionLifecycleAction action,
        DateTimeOffset occurredAtUtc)
    {
        switch (action)
        {
            case LiveSessionLifecycleAction.Start:
                liveSession.Start(occurredAtUtc);
                return Array.Empty<ReleasedHint>();
            case LiveSessionLifecycleAction.Pause:
                liveSession.Pause();
                return Array.Empty<ReleasedHint>();
            case LiveSessionLifecycleAction.Resume:
                liveSession.Resume();
                return Array.Empty<ReleasedHint>();
            case LiveSessionLifecycleAction.Finalize:
                return liveSession.FinalizeAndRevealAllHints(occurredAtUtc);
            case LiveSessionLifecycleAction.Cancel:
                liveSession.Cancel();
                return Array.Empty<ReleasedHint>();
            default:
                throw new UmbralDomainException(
                    "live_session_lifecycle_action_invalid",
                    $"Unsupported lifecycle action '{action}'.",
                    UmbralFailureCategory.Validation);
        }
    }

    private async Task PublishHintUnlockedAsync(
        LiveSession liveSession,
        ReleasedHint releasedHint,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = new HintUnlockedPayload(
            new RealtimeEventMetadata(
                liveSession.Id,
                liveSession.SequenceNumber,
                occurredAtUtc,
                SnapshotRefreshPolicy.RefreshSnapshot,
                "LiveSession finalized; all Hints and solutions revealed."),
            releasedHint.SessionTeamId,
            MapVisibleHint(liveSession, releasedHint));

        await hubContext.Clients.All.ReceiveHintUnlocked(payload).WaitAsync(cancellationToken);
    }

    private static VisibleHintSnapshot MapVisibleHint(LiveSession liveSession, ReleasedHint releasedHint)
    {
        var sessionStage = liveSession.SessionStageFlow.Single(stage =>
            stage.MissionStageId == releasedHint.MissionStageId);
        var hint = sessionStage.Hints.Single(stageHint => stageHint.Id == releasedHint.HintId);

        return new VisibleHintSnapshot(
            hint.Id,
            sessionStage.MissionStageId,
            hint.Content,
            hint.IsSolution,
            hint.Latitude,
            hint.Longitude,
            releasedHint.ReleasedAtUtc,
            releasedHint.UnlockReason);
    }
}

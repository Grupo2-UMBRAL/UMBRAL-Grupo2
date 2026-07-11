using SessionManagement.Domain.LiveSessions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.Application.Features.SessionSnapshots;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionLifecycle;

public sealed class TransitionLiveSessionStateCommandHandler(
    IUnitOfWork unitOfWork, IRepository<LiveSession> liveSessionRepository,
    TimeProvider timeProvider,
    ISessionRealtimeNotifier realtimeNotifier)
    : IRequestHandler<TransitionLiveSessionStateCommand, LiveSessionStateResponse>
{
    public async Task<LiveSessionStateResponse> Handle(
        TransitionLiveSessionStateCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository
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

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = liveSession.ToStateResponse();

        await realtimeNotifier.NotifySessionStateChangedAsync(
            new LiveSessionStateChangedEvent(
                liveSession.Id,
                previousState.Value,
                liveSession.State.Value,
                liveSession.SessionTeams.Count,
                liveSession.SequenceNumber,
                "LiveSession state changed.",
                occurredAtUtc),
            cancellationToken);

        foreach (var releasedHint in newlyReleasedHints)
        {
            await realtimeNotifier.NotifyHintUnlockedAsync(
                new HintUnlockedPayload(
                    new RealtimeEventMetadata(
                        liveSession.Id,
                        liveSession.SequenceNumber,
                        occurredAtUtc,
                        SnapshotRefreshPolicy.RefreshSnapshot,
                        "LiveSession finalized; all Hints and solutions revealed."),
                    releasedHint.SessionTeamId,
                    MapVisibleHint(liveSession, releasedHint)),
                cancellationToken);
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




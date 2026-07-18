using MediatR;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed class DeactivateStageHandler(
    ILiveSessionRepository liveSessionRepository,
    TimeProvider timeProvider,
    ISessionRealtimeNotifier realtimeNotifier)
    : IRequestHandler<DeactivateStageCommand, LiveSessionResponse>
{
    public async Task<LiveSessionResponse> Handle(
        DeactivateStageCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository.GetForSessionFlowDeactivationAsync(request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                $"LiveSession '{request.LiveSessionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        var previousState = liveSession.State;
        var updatedAtUtc = timeProvider.GetUtcNow();
        liveSession.DeactivateStage(request.MissionStageId, updatedAtUtc);

        await liveSessionRepository.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.NotifySessionStateChangedAsync(
            new LiveSessionStateChangedEvent(
                liveSession.Id,
                previousState.Name,
                liveSession.State.Name,
                liveSession.SessionTeams.Count,
                liveSession.SequenceNumber,
                "Session Stage Flow changed; refresh LiveSession snapshot.",
                updatedAtUtc),
            cancellationToken);

        return liveSession.ToResponse();
    }
}




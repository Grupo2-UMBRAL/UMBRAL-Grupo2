using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.Realtime;
using Umbral.SessionOperations.Api.Application.SessionLifecycle;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.LiveSessions;

public sealed record DeactivateStageCommand(Guid LiveSessionId, Guid MissionStageId) : IRequest<LiveSessionResponse>;

public sealed class DeactivateStageHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider,
    ISessionRealtimeNotifier realtimeNotifier)
    : IRequestHandler<DeactivateStageCommand, LiveSessionResponse>
{
    public async Task<LiveSessionResponse> Handle(
        DeactivateStageCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamProgressions)
            .SingleOrDefaultAsync(session => session.Id == request.LiveSessionId, cancellationToken);
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

        await dbContext.SaveChangesAsync(cancellationToken);
        await realtimeNotifier.NotifySessionStateChangedAsync(
            new LiveSessionStateChangedEvent(
                liveSession.Id,
                previousState,
                liveSession.State,
                liveSession.SessionTeams.Count,
                liveSession.SequenceNumber,
                "Session Stage Flow changed; refresh LiveSession snapshot.",
                updatedAtUtc),
            cancellationToken);

        return liveSession.ToResponse();
    }
}

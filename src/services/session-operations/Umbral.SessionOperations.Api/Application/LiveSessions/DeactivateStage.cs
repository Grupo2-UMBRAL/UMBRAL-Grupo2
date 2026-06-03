using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Hubs;
using Umbral.SessionOperations.Api.Hubs.Contracts;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.LiveSessions;

public sealed record DeactivateStageCommand(Guid LiveSessionId, Guid MissionStageId) : IRequest<LiveSessionResponse>;

public sealed class DeactivateStageHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<SessionOperationsHub, ISessionClient> hubContext)
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
        await PublishSessionStateChangedAsync(liveSession, previousState, updatedAtUtc, cancellationToken);

        return liveSession.ToResponse();
    }

    private async Task PublishSessionStateChangedAsync(
        LiveSession liveSession,
        string previousState,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var payload = new SessionStateChangedPayload(
            new RealtimeEventMetadata(
                liveSession.Id,
                liveSession.SequenceNumber,
                occurredAtUtc,
                SnapshotRefreshPolicy.RefreshSnapshot,
                "Session Stage Flow changed; refresh LiveSession snapshot."),
            previousState,
            liveSession.State,
            null);

        await hubContext.Clients.All.ReceiveSessionStateChanged(payload).WaitAsync(cancellationToken);
    }
}

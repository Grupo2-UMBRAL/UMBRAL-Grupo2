using MediatR;
using SessionManagement.Application.Abstractions;
using SessionManagement.Application.Features.SessionLifecycle;
using SessionManagement.Application.Abstractions.Realtime;
using SessionManagement.Domain.LiveSessions;
using Umbral.ServiceDefaults;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class RegisterTeamByOperatorHandler(
    ILiveSessionRepository liveSessionRepository,
    TimeProvider timeProvider,
    ISessionRealtimeNotifier realtimeNotifier)
    : IRequestHandler<RegisterTeamByOperatorCommand, RegisterTeamByOperatorResponse>
{
    public async Task<RegisterTeamByOperatorResponse> Handle(
        RegisterTeamByOperatorCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository.GetWithSessionTeamsAsync(request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                $"LiveSession '{request.LiveSessionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        var createdAtUtc = timeProvider.GetUtcNow();
        var sessionTeam = liveSession.RegisterTeamByOperator(Guid.NewGuid(), request.TeamName, createdAtUtc);

        await liveSessionRepository.SaveChangesAsync(cancellationToken);

        // Let connected clients (operator panel + player lobbies) refresh their rosters.
        await realtimeNotifier.NotifySessionStateChangedAsync(
            new LiveSessionStateChangedEvent(
                liveSession.Id,
                liveSession.State.Name,
                liveSession.State.Name,
                liveSession.SessionTeams.Count,
                liveSession.SequenceNumber,
                "Session Team roster changed; refresh LiveSession snapshot.",
                createdAtUtc),
            cancellationToken);

        return new RegisterTeamByOperatorResponse(
            liveSession.Id,
            sessionTeam.Id,
            sessionTeam.Name,
            sessionTeam.CreatedAtUtc);
    }
}

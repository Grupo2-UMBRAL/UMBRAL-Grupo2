using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class ListSessionTeamsHandler(
    IRepository<LiveSession> liveSessionRepository,
    TimeProvider timeProvider)
    : IRequestHandler<ListSessionTeamsQuery, SessionTeamsResponse>
{
    public async Task<SessionTeamsResponse> Handle(
        ListSessionTeamsQuery request,
        CancellationToken cancellationToken)
    {
        var joinCode = JoinCode.Parse(request.JoinCode);
        var liveSession = await liveSessionRepository
            .Include(session => session.SessionTeams)
            .SingleOrDefaultAsync(session => session.JoinCodeValue == joinCode.Value, cancellationToken);
        if (liveSession is null)
        {
            throw CreateInvalidJoinCodeException();
        }

        var sessionTeams = liveSession.SessionTeams
            .OrderBy(team => team.Name, StringComparer.OrdinalIgnoreCase)
            .Select(team => new SessionTeamResponse(team.Id, team.Name))
            .ToArray();

        return new SessionTeamsResponse(
            liveSession.Id,
            liveSession.IsEnrollmentOpenAt(timeProvider.GetUtcNow()),
            sessionTeams);
    }

    private static UmbralDomainException CreateInvalidJoinCodeException()
        => new(
            "join_code_invalid_for_live_session",
            "Join Code is invalid for this LiveSession.",
            UmbralFailureCategory.NotFound);
}




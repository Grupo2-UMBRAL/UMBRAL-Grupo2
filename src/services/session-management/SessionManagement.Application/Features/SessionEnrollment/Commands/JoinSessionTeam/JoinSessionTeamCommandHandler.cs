using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Domain.LiveSessions;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class JoinSessionTeamHandler(
    ISessionManagementDbContext dbContext,
    TimeProvider timeProvider,
    ICurrentParticipantIdentity currentParticipantIdentity)
    : IRequestHandler<JoinSessionTeamCommand, JoinSessionTeamResponse>
{
    public async Task<JoinSessionTeamResponse> Handle(
        JoinSessionTeamCommand request,
        CancellationToken cancellationToken)
    {
        var joinCode = JoinCode.Parse(request.JoinCode);
        var participantUserId = currentParticipantIdentity.GetRequiredParticipantUserId();
        var liveSession = await dbContext.LiveSessions
            .Include(session => session.SessionTeams)
            .Include(session => session.TeamParticipations)
            .SingleOrDefaultAsync(session => session.JoinCodeValue == joinCode.Value, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "join_code_invalid_for_live_session",
                "Join Code is invalid for this LiveSession.",
                UmbralFailureCategory.NotFound);
        }

        var enrolledAtUtc = timeProvider.GetUtcNow();
        liveSession.EnrollParticipantInTeam(
            request.SessionTeamId,
            participantUserId.Value,
            joinCode,
            enrolledAtUtc);

        await dbContext.SaveChangesAsync(cancellationToken);

        var sessionTeam = liveSession.SessionTeams.Single(team => team.Id == request.SessionTeamId);
        return new JoinSessionTeamResponse(
            liveSession.Id,
            sessionTeam.Id,
            sessionTeam.Name,
            participantUserId.Value,
            enrolledAtUtc);
    }
}

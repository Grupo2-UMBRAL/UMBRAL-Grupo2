using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.SessionEnrollment;

public sealed record RegisterTeamCommand(string JoinCode, string TeamName) : IRequest<RegisterTeamResponse>;

public sealed class RegisterTeamHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider,
    ICurrentParticipantIdentity currentParticipantIdentity)
    : IRequestHandler<RegisterTeamCommand, RegisterTeamResponse>
{
    public async Task<RegisterTeamResponse> Handle(
        RegisterTeamCommand request,
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

        var nowUtc = timeProvider.GetUtcNow();
        var sessionTeam = liveSession.RegisterTeam(
            Guid.NewGuid(),
            request.TeamName,
            participantUserId.Value,
            joinCode,
            nowUtc);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterTeamResponse(
            liveSession.Id,
            sessionTeam.Id,
            sessionTeam.Name,
            participantUserId.Value,
            nowUtc);
    }
}

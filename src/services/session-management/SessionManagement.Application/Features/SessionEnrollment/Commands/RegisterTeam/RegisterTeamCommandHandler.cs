using SessionManagement.Domain.LiveSessions;
using MediatR;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class RegisterTeamHandler(
    ILiveSessionRepository liveSessionRepository,
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

        var liveSession = await liveSessionRepository.GetByJoinCodeWithEnrollmentAsync(joinCode.Value, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "join_code_invalid_for_live_session",
                "Join Code is invalid for this LiveSession.",
                UmbralFailureCategory.NotFound);
        }

        var nowUtc = timeProvider.GetUtcNow();

        // Orchestrate the use case: create the team, then enrol the participant into it.
        // The Domain exposes both as granular operations; sequencing them is the Handler's job.
        var sessionTeam = liveSession.RegisterTeam(
            Guid.NewGuid(),
            request.TeamName,
            joinCode,
            nowUtc);
        liveSession.EnrollParticipantInTeam(
            sessionTeam.Id,
            participantUserId.Value,
            joinCode,
            nowUtc);

        await liveSessionRepository.SaveChangesAsync(cancellationToken);

        return new RegisterTeamResponse(
            liveSession.Id,
            sessionTeam.Id,
            sessionTeam.Name,
            participantUserId.Value,
            nowUtc);
    }
}




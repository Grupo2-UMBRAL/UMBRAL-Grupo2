using SessionManagement.Domain.LiveSessions;
using MediatR;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class GenerateJoinCodeHandler(
    ILiveSessionRepository liveSessionRepository,
    IJoinCodeGenerator joinCodeGenerator)
    : IRequestHandler<GenerateJoinCodeCommand, GenerateJoinCodeResponse>
{
    public async Task<GenerateJoinCodeResponse> Handle(
        GenerateJoinCodeCommand request,
        CancellationToken cancellationToken)
    {
        var liveSession = await liveSessionRepository.GetAsync(request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                "LiveSession was not found.",
                UmbralFailureCategory.NotFound);
        }

        if (liveSession.JoinCodeValue is not null)
        {
            return new GenerateJoinCodeResponse(liveSession.Id, liveSession.JoinCodeValue);
        }

        var joinCode = joinCodeGenerator.Generate();
        liveSession.AssignJoinCode(joinCode);
        await liveSessionRepository.SaveChangesAsync(cancellationToken);
        return new GenerateJoinCodeResponse(liveSession.Id, joinCode.Value);
    }
}




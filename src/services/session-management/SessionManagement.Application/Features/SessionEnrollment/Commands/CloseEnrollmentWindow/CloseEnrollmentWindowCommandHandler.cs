using SessionManagement.Domain.LiveSessions;
using MediatR;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class CloseEnrollmentWindowHandler(
    ILiveSessionRepository liveSessionRepository,
    TimeProvider timeProvider)
    : IRequestHandler<CloseEnrollmentWindowCommand, EnrollmentWindowResponse>
{
    public async Task<EnrollmentWindowResponse> Handle(
        CloseEnrollmentWindowCommand request,
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

        var nowUtc = timeProvider.GetUtcNow();
        liveSession.CloseEnrollmentWindow(nowUtc);
        await liveSessionRepository.SaveChangesAsync(cancellationToken);

        return new EnrollmentWindowResponse(
            liveSession.Id,
            liveSession.JoinCodeValue,
            liveSession.EnrollmentWindowOpenedAtUtc,
            liveSession.EnrollmentWindowClosedAtUtc,
            liveSession.IsEnrollmentOpenAt(nowUtc));
    }
}




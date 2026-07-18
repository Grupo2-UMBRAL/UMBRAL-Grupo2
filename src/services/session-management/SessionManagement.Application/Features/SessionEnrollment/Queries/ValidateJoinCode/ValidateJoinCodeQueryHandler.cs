using SessionManagement.Domain.LiveSessions;
using MediatR;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed class ValidateJoinCodeHandler(
    ILiveSessionReadRepository liveSessionRepository,
    TimeProvider timeProvider)
    : IRequestHandler<ValidateJoinCodeQuery, ParticipantEnrollmentStatusResponse>
{
    public async Task<ParticipantEnrollmentStatusResponse> Handle(
        ValidateJoinCodeQuery request,
        CancellationToken cancellationToken)
    {
        var joinCode = JoinCode.Parse(request.JoinCode);
        var liveSession = await liveSessionRepository.GetByJoinCodeAsync(joinCode.Value, cancellationToken);
        if (liveSession is null)
        {
            throw CreateInvalidJoinCodeException();
        }

        return new ParticipantEnrollmentStatusResponse(
            liveSession.Id,
            liveSession.State.Value,
            liveSession.EnrollmentWindowOpenedAtUtc,
            liveSession.EnrollmentWindowClosedAtUtc,
            liveSession.IsEnrollmentOpenAt(timeProvider.GetUtcNow()));
    }

    private static UmbralDomainException CreateInvalidJoinCodeException()
        => new(
            "join_code_invalid_for_live_session",
            "Join Code is invalid for this LiveSession.",
            UmbralFailureCategory.NotFound);
}




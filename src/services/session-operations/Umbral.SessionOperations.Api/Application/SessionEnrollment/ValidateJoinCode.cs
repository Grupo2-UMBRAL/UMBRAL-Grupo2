using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Domain.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.SessionEnrollment;

public sealed record ValidateJoinCodeQuery(string JoinCode) : IRequest<ParticipantEnrollmentStatusResponse>;

public sealed class ValidateJoinCodeHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider)
    : IRequestHandler<ValidateJoinCodeQuery, ParticipantEnrollmentStatusResponse>
{
    public async Task<ParticipantEnrollmentStatusResponse> Handle(
        ValidateJoinCodeQuery request,
        CancellationToken cancellationToken)
    {
        var joinCode = JoinCode.Parse(request.JoinCode);
        var liveSession = await dbContext.LiveSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(session => session.JoinCodeValue == joinCode.Value, cancellationToken);
        if (liveSession is null)
        {
            throw CreateInvalidJoinCodeException();
        }

        return new ParticipantEnrollmentStatusResponse(
            liveSession.Id,
            liveSession.State,
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

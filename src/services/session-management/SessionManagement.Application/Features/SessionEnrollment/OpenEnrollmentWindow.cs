using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.SessionEnrollment;

public sealed record OpenEnrollmentWindowCommand(Guid LiveSessionId) : IRequest<EnrollmentWindowResponse>;

public sealed class OpenEnrollmentWindowHandler(
    ISessionManagementDbContext dbContext,
    TimeProvider timeProvider)
    : IRequestHandler<OpenEnrollmentWindowCommand, EnrollmentWindowResponse>
{
    public async Task<EnrollmentWindowResponse> Handle(
        OpenEnrollmentWindowCommand request,
        CancellationToken cancellationToken)
    {
        if (request.LiveSessionId == Guid.Empty)
        {
            throw new UmbralDomainException(
                "live_session_id_required",
                "LiveSession id is required.",
                UmbralFailureCategory.Validation);
        }

        var liveSession = await dbContext.LiveSessions
            .SingleOrDefaultAsync(session => session.Id == request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                "LiveSession was not found.",
                UmbralFailureCategory.NotFound);
        }

        var nowUtc = timeProvider.GetUtcNow();
        liveSession.OpenEnrollmentWindow(nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new EnrollmentWindowResponse(
            liveSession.Id,
            liveSession.JoinCodeValue,
            liveSession.EnrollmentWindowOpenedAtUtc,
            liveSession.EnrollmentWindowClosedAtUtc,
            liveSession.IsEnrollmentOpenAt(nowUtc));
    }
}

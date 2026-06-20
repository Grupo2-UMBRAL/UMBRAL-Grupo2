using SessionManagement.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Features.LiveSessions;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.Hints;

public sealed class CreateOperationalHintHandler(
    IUnitOfWork unitOfWork, IRepository<LiveSession> liveSessionRepository,
    TimeProvider timeProvider)
    : IRequestHandler<CreateOperationalHintCommand, LiveSessionStageHintResponse>
{
    public async Task<LiveSessionStageHintResponse> Handle(
        CreateOperationalHintCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository
            .SingleOrDefaultAsync(session => session.Id == request.LiveSessionId, cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                $"LiveSession '{request.LiveSessionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        var createdAtUtc = timeProvider.GetUtcNow();
        var operationalHint = liveSession.AddOperationalHint(
            request.MissionStageId,
            request.Content,
            request.Latitude,
            request.Longitude,
            createdAtUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LiveSessionStageHintResponse(
            operationalHint.Id,
            operationalHint.Content,
            operationalHint.IsSolution,
            operationalHint.Latitude,
            operationalHint.Longitude);
    }
}




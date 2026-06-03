using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Application.LiveSessions;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.Hints;

public sealed record CreateOperationalHintCommand(
    Guid LiveSessionId,
    Guid MissionStageId,
    string Content,
    double? Latitude,
    double? Longitude) : IRequest<LiveSessionStageHintResponse>;

public sealed record CreateOperationalHintRequest(string Content, double? Latitude, double? Longitude);

public sealed class CreateOperationalHintHandler(
    SessionOperationsDbContext dbContext,
    TimeProvider timeProvider)
    : IRequestHandler<CreateOperationalHintCommand, LiveSessionStageHintResponse>
{
    public async Task<LiveSessionStageHintResponse> Handle(
        CreateOperationalHintCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LiveSessionStageHintResponse(
            operationalHint.Id,
            operationalHint.Content,
            operationalHint.IsSolution,
            operationalHint.Latitude,
            operationalHint.Longitude);
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionOperations.Application.Abstractions;

namespace SessionOperations.Application.Features.LiveSessions;

public sealed record GetLiveSessionByIdQuery(Guid LiveSessionId) : IRequest<LiveSessionResponse>
{
}

public sealed class GetLiveSessionByIdQueryHandler(ISessionOperationsDbContext dbContext)
    : IRequestHandler<GetLiveSessionByIdQuery, LiveSessionResponse>
{
    public async Task<LiveSessionResponse> Handle(
        GetLiveSessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
            .AsNoTracking()
            .Include(existingLiveSession => existingLiveSession.SessionTeams)
            .SingleOrDefaultAsync(
                existingLiveSession => existingLiveSession.Id == request.LiveSessionId,
                cancellationToken);
        if (liveSession is null)
        {
            throw new UmbralDomainException(
                "live_session_not_found",
                $"LiveSession '{request.LiveSessionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        return liveSession.ToResponse();
    }
}

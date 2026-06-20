using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed class GetLiveSessionByIdQueryHandler(IUnitOfWork unitOfWork, IRepository<LiveSession> liveSessionRepository)
    : IRequestHandler<GetLiveSessionByIdQuery, LiveSessionResponse>
{
    public async Task<LiveSessionResponse> Handle(
        GetLiveSessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository
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



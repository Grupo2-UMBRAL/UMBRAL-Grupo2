using SessionManagement.Domain.LiveSessions;
using MediatR;
using Umbral.ServiceDefaults;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed class GetLiveSessionByIdQueryHandler(ILiveSessionReadRepository liveSessionRepository)
    : IRequestHandler<GetLiveSessionByIdQuery, LiveSessionResponse>
{
    public async Task<LiveSessionResponse> Handle(
        GetLiveSessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await liveSessionRepository.GetByIdWithSessionTeamsAsync(request.LiveSessionId, cancellationToken);
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




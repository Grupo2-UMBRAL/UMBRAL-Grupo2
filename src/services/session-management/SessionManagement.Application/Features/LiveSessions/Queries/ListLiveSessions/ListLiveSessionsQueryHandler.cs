using SessionManagement.Domain.LiveSessions;
using MediatR;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed class ListLiveSessionsQueryHandler(ILiveSessionReadRepository liveSessionRepository)
    : IRequestHandler<ListLiveSessionsQuery, IReadOnlyList<LiveSessionResponse>>
{
    public async Task<IReadOnlyList<LiveSessionResponse>> Handle(
        ListLiveSessionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSessions = await liveSessionRepository.ListWithSessionTeamsAsync(cancellationToken);

        return liveSessions
            .Select(liveSession => liveSession.ToResponse())
            .ToArray();
    }
}




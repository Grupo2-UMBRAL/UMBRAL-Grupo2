using SessionManagement.Domain.LiveSessions;
using Umbral.ServiceDefaults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed class ListLiveSessionsQueryHandler(IUnitOfWork unitOfWork, IRepository<LiveSession> liveSessionRepository)
    : IRequestHandler<ListLiveSessionsQuery, IReadOnlyList<LiveSessionResponse>>
{
    public async Task<IReadOnlyList<LiveSessionResponse>> Handle(
        ListLiveSessionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSessions = await liveSessionRepository
            .Include(liveSession => liveSession.SessionTeams)
            .OrderByDescending(liveSession => liveSession.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return liveSessions
            .Select(liveSession => liveSession.ToResponse())
            .ToArray();
    }
}




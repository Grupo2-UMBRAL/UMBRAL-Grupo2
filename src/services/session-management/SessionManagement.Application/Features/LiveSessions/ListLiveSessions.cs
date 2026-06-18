using Umbral.ServiceDefaults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SessionManagement.Application.Abstractions;

namespace SessionManagement.Application.Features.LiveSessions;

public sealed record ListLiveSessionsQuery : IRequest<IReadOnlyList<LiveSessionResponse>>
{
}

public sealed class ListLiveSessionsQueryHandler(ISessionManagementDbContext dbContext)
    : IRequestHandler<ListLiveSessionsQuery, IReadOnlyList<LiveSessionResponse>>
{
    public async Task<IReadOnlyList<LiveSessionResponse>> Handle(
        ListLiveSessionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSessions = await dbContext.LiveSessions
            .AsNoTracking()
            .Include(liveSession => liveSession.SessionTeams)
            .OrderByDescending(liveSession => liveSession.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return liveSessions
            .Select(liveSession => liveSession.ToResponse())
            .ToArray();
    }
}

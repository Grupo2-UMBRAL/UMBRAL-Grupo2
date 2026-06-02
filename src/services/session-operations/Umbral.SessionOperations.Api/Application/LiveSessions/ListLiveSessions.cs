using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.LiveSessions;

public sealed record ListLiveSessionsQuery : IRequest<IReadOnlyList<LiveSessionResponse>>;

public sealed class ListLiveSessionsQueryHandler(SessionOperationsDbContext dbContext)
    : IRequestHandler<ListLiveSessionsQuery, IReadOnlyList<LiveSessionResponse>>
{
    public async Task<IReadOnlyList<LiveSessionResponse>> Handle(
        ListLiveSessionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSessions = await dbContext.LiveSessions
            .AsNoTracking()
            .OrderByDescending(liveSession => liveSession.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return liveSessions
            .Select(liveSession => liveSession.ToResponse())
            .ToArray();
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;
using Umbral.SessionOperations.Api.Infrastructure;

namespace Umbral.SessionOperations.Api.Application.LiveSessions;

public sealed record GetLiveSessionByIdQuery(Guid LiveSessionId) : IRequest<LiveSessionResponse>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOrOperator;
}

public sealed class GetLiveSessionByIdQueryHandler(SessionOperationsDbContext dbContext)
    : IRequestHandler<GetLiveSessionByIdQuery, LiveSessionResponse>
{
    public async Task<LiveSessionResponse> Handle(
        GetLiveSessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var liveSession = await dbContext.LiveSessions
            .AsNoTracking()
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

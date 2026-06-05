using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record ListMissionsQuery : IRequest<IReadOnlyList<MissionSummaryResponse>>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
}

public sealed class ListMissionsQueryHandler(MissionDesignDbContext dbContext)
    : IRequestHandler<ListMissionsQuery, IReadOnlyList<MissionSummaryResponse>>
{
    public async Task<IReadOnlyList<MissionSummaryResponse>> Handle(
        ListMissionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await dbContext.Missions
            .AsNoTracking()
            .OrderBy(mission => mission.Name)
            .Select(mission => mission.ToSummaryResponse())
            .ToListAsync(cancellationToken);
    }
}

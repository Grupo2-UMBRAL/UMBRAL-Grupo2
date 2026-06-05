using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record ListEligibleMissionsForLiveSessionQuery : IRequest<IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOrOperator;
}

public sealed class ListEligibleMissionsForLiveSessionQueryHandler(MissionDesignDbContext dbContext)
    : IRequestHandler<ListEligibleMissionsForLiveSessionQuery, IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>>
{
    public async Task<IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>> Handle(
        ListEligibleMissionsForLiveSessionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missions = await dbContext.Missions
            .AsNoTracking()
            .Where(mission => mission.IsActive)
            .OrderBy(mission => mission.Name)
            .ToListAsync(cancellationToken);

        return missions
            .Where(mission => mission.IsEligibleForLiveSession())
            .Select(mission => mission.ToEligibleForLiveSessionSummaryResponse())
            .ToArray();
    }
}

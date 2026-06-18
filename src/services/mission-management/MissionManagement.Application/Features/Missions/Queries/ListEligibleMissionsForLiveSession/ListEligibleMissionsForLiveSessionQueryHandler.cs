using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MissionManagement.Application.Features.Missions.Queries.ListEligibleMissionsForLiveSession;

public sealed class ListEligibleMissionsForLiveSessionQueryHandler(IMissionManagementDbContext dbContext)
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

using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MissionDesign.Application.Features.Missions.Queries.ListMissions;

public sealed class ListMissionsQueryHandler(IMissionDesignDbContext dbContext)
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

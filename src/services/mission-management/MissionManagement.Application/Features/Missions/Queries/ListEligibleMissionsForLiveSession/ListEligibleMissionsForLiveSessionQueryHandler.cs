using MissionManagement.Domain.Missions;
using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MissionManagement.Application.Features.Missions.Queries.ListEligibleMissionsForLiveSession;

public sealed class ListEligibleMissionsForLiveSessionQueryHandler(
    IRepository<Mission> missionRepository,
    IMissionManagementDbContext dbContext)
    : IRequestHandler<ListEligibleMissionsForLiveSessionQuery, IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>>
{
    public async Task<IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>> Handle(
        ListEligibleMissionsForLiveSessionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var activeMissionIds = await missionRepository
            .Where(mission => mission.IsActive)
            .OrderBy(mission => mission.Name)
            .Select(mission => mission.Id)
            .ToListAsync(cancellationToken);

        var summaries = new List<EligibleMissionForLiveSessionSummaryResponse>();
        foreach (var missionId in activeMissionIds)
        {
            var mission = await MissionLoader.LoadAsync(dbContext, missionId, cancellationToken);
            if (mission is null || !mission.IsEligibleForLiveSession())
            {
                continue;
            }

            summaries.Add(mission.ToEligibleForLiveSessionSummaryResponse());
        }

        return summaries;
    }
}

using MissionManagement.Domain.Missions;
using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MissionManagement.Application.Features.Missions.Queries.ListEligibleMissionsForLiveSession;

public sealed class ListEligibleMissionsForLiveSessionQueryHandler(IUnitOfWork unitOfWork, IRepository<Mission> missionRepository)
    : IRequestHandler<ListEligibleMissionsForLiveSessionQuery, IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>>
{
    public async Task<IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>> Handle(
        ListEligibleMissionsForLiveSessionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missions = await missionRepository
            .Where(mission => mission.IsActive)
            .OrderBy(mission => mission.Name)
            .ToListAsync(cancellationToken);

        return missions
            .Where(mission => mission.IsEligibleForLiveSession())
            .Select(mission => mission.ToEligibleForLiveSessionSummaryResponse())
            .ToArray();
    }
}





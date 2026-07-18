using MissionManagement.Application.Abstractions;
using MediatR;

namespace MissionManagement.Application.Features.Missions.Queries.ListEligibleMissionsForLiveSession;

public sealed class ListEligibleMissionsForLiveSessionQueryHandler(IMissionRepository missionRepository)
    : IRequestHandler<ListEligibleMissionsForLiveSessionQuery, IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>>
{
    public async Task<IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>> Handle(
        ListEligibleMissionsForLiveSessionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eligibleMissions = await missionRepository.ListEligibleMissionsAsync(cancellationToken);
        return eligibleMissions.Select(mission => mission.ToEligibleForLiveSessionSummaryResponse()).ToList();
    }
}

using MissionManagement.Application.Abstractions;
using MediatR;

namespace MissionManagement.Application.Features.Missions.Queries.ListMissions;

public sealed class ListMissionsQueryHandler(IMissionRepository missionRepository)
    : IRequestHandler<ListMissionsQuery, IReadOnlyList<MissionSummaryResponse>>
{
    public async Task<IReadOnlyList<MissionSummaryResponse>> Handle(
        ListMissionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missions = await missionRepository.ListMissionsAsync(cancellationToken);
        return missions.Select(mission => mission.ToSummaryResponse()).ToList();
    }
}

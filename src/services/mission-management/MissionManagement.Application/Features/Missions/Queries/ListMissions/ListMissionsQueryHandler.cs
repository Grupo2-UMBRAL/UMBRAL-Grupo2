using MissionManagement.Domain.Missions;
using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MissionManagement.Application.Features.Missions.Queries.ListMissions;

public sealed class ListMissionsQueryHandler(IUnitOfWork unitOfWork, IRepository<Mission> missionRepository)
    : IRequestHandler<ListMissionsQuery, IReadOnlyList<MissionSummaryResponse>>
{
    public async Task<IReadOnlyList<MissionSummaryResponse>> Handle(
        ListMissionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await missionRepository
            .OrderBy(mission => mission.Name)
            .Select(mission => mission.ToSummaryResponse())
            .ToListAsync(cancellationToken);
    }
}




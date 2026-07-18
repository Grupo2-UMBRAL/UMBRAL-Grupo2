using MissionManagement.Application.Abstractions;
using MediatR;

namespace MissionManagement.Application.Features.Missions.Queries.GetMissionById;

public sealed class GetMissionByIdQueryHandler(IMissionRepository missionRepository)
    : IRequestHandler<GetMissionByIdQuery, MissionResponse>
{
    public async Task<MissionResponse> Handle(GetMissionByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mission = await missionRepository.GetRequiredWithItemsAsync(request.MissionId, cancellationToken);

        return mission.ToResponse();
    }
}

using MediatR;

namespace MissionManagement.Application.Features.Missions.Commands.UpdateMission;

public sealed record UpdateMissionCommand(
    Guid MissionId,
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<MissionItemRequest>? Items = null) : IRequest<MissionResponse>
{
}

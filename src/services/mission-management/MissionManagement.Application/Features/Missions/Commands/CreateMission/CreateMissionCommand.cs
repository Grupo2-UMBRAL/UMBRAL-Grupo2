using MediatR;

namespace MissionManagement.Application.Features.Missions.Commands.CreateMission;

public sealed record CreateMissionCommand(
    string Name,
    string Description,
    int MaximumDurationMinutes,
    IReadOnlyList<MissionItemRequest>? Items = null) : IRequest<MissionResponse>
{
}

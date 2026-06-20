using MediatR;

namespace MissionManagement.Application.Features.Missions.Commands.UpdateMission;

public sealed record UpdateMissionCommand(
    Guid MissionId,
    string Name,
    string Description,
    string Difficulty,
    int MaximumDurationMinutes,
    string GameType,
    IReadOnlyList<MissionNodeRequest>? Nodes = null) : IRequest<MissionResponse>
{
}


using MediatR;

namespace MissionManagement.Application.Features.Missions.Commands.CreateMission;

public sealed record CreateMissionCommand(
    string Name,
    string Description,
    string Difficulty,
    int MaximumDurationMinutes,
    string GameType,
    IReadOnlyList<MissionNodeRequest>? Nodes = null) : IRequest<MissionResponse>
{
}


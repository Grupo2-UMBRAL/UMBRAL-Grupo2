using MediatR;

namespace MissionManagement.Application.Features.Missions.Commands.ActivateMission;

public sealed record ActivateMissionCommand(Guid MissionId) : IRequest<MissionResponse>
{
}


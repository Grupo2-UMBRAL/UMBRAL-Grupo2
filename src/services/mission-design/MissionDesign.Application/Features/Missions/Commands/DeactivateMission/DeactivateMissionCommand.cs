using MediatR;

namespace MissionDesign.Application.Features.Missions.Commands.DeactivateMission;

public sealed record DeactivateMissionCommand(Guid MissionId) : IRequest<MissionResponse>
{
}

using MediatR;

namespace MissionManagement.Application.Features.MissionStages.Commands.DeactivateMissionStage;

public sealed record DeactivateMissionStageCommand(Guid MissionStageId) : IRequest<MissionStageResponse>
{
}


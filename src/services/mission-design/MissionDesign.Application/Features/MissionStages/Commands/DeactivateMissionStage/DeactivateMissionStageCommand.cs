using MediatR;

namespace MissionDesign.Application.Features.MissionStages.Commands.DeactivateMissionStage;

public sealed record DeactivateMissionStageCommand(Guid MissionStageId) : IRequest<MissionStageResponse>
{
}

using MediatR;

namespace MissionManagement.Application.Features.MissionStages.Commands.CreateMissionStageHint;

public sealed record CreateMissionStageHintCommand(
    Guid MissionStageId,
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude) : IRequest<MissionStageHintResponse>
{
}


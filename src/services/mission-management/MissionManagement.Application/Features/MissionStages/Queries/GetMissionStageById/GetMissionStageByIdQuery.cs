using MediatR;

namespace MissionManagement.Application.Features.MissionStages.Queries.GetMissionStageById;

public sealed record GetMissionStageByIdQuery(Guid MissionStageId) : IRequest<MissionStageResponse>
{
}


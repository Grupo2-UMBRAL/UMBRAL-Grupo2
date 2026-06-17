using MediatR;

namespace MissionDesign.Application.Features.MissionStages.Queries.GetMissionStageById;

public sealed record GetMissionStageByIdQuery(Guid MissionStageId) : IRequest<MissionStageResponse>
{
}

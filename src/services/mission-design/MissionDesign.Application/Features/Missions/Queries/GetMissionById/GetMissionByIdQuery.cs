using MediatR;

namespace MissionDesign.Application.Features.Missions.Queries.GetMissionById;

public sealed record GetMissionByIdQuery(Guid MissionId) : IRequest<MissionResponse>
{
}

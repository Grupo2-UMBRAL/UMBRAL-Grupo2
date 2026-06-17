using MediatR;

namespace MissionDesign.Application.Features.Missions.Queries.GetEligibleMissionForLiveSession;

public sealed record GetEligibleMissionForLiveSessionQuery(Guid MissionId) : IRequest<EligibleMissionForLiveSessionResponse>
{
}

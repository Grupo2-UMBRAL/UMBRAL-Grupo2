using MediatR;

namespace MissionManagement.Application.Features.Missions.Queries.GetEligibleMissionForLiveSession;

public sealed record GetEligibleMissionForLiveSessionQuery(Guid MissionId) : IRequest<EligibleMissionForLiveSessionResponse>
{
}


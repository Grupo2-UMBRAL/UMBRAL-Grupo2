using MediatR;

namespace MissionDesign.Application.Features.Missions.Queries.ListEligibleMissionsForLiveSession;

public sealed record ListEligibleMissionsForLiveSessionQuery : IRequest<IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>>
{
}

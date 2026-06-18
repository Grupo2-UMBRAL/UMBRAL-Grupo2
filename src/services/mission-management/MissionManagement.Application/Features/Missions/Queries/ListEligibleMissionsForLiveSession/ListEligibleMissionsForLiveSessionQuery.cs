using MediatR;

namespace MissionManagement.Application.Features.Missions.Queries.ListEligibleMissionsForLiveSession;

public sealed record ListEligibleMissionsForLiveSessionQuery : IRequest<IReadOnlyList<EligibleMissionForLiveSessionSummaryResponse>>
{
}

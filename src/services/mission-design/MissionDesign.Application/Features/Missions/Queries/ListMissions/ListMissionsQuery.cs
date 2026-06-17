using MediatR;

namespace MissionDesign.Application.Features.Missions.Queries.ListMissions;

public sealed record ListMissionsQuery : IRequest<IReadOnlyList<MissionSummaryResponse>>
{
}

using MediatR;

namespace MissionManagement.Application.Features.Missions.Queries.ListMissions;

public sealed record ListMissionsQuery : IRequest<IReadOnlyList<MissionSummaryResponse>>
{
}


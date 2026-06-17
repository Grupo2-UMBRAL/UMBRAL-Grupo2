using MediatR;

namespace MissionDesign.Application.Features.MissionStages.Queries.ListMissionStages;

public sealed record ListMissionStagesQuery(Guid MissionId) : IRequest<IReadOnlyList<MissionStageSummaryResponse>>
{
}

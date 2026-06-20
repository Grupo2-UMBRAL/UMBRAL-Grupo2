using MissionManagement.Domain.Entities;
using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MissionManagement.Application.Features.MissionStages.Queries.ListMissionStages;

public sealed class ListMissionStagesQueryHandler(IUnitOfWork unitOfWork, IRepository<MissionStage> missionStageRepository)
    : IRequestHandler<ListMissionStagesQuery, IReadOnlyList<MissionStageSummaryResponse>>
{
    public async Task<IReadOnlyList<MissionStageSummaryResponse>> Handle(
        ListMissionStagesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missionStages = await missionStageRepository
            .Where(missionStage => missionStage.MissionId == request.MissionId)
            .OrderBy(missionStage => missionStage.Order)
            .ThenBy(missionStage => missionStage.Name)
            .Include(missionStage => missionStage.Hints)
            .ToListAsync(cancellationToken);

        return missionStages
            .Select(missionStage => missionStage.ToSummaryResponse())
            .ToList();
    }
}





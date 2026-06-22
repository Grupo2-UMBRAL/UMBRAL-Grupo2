using MissionManagement.Domain.Missions;
using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.MissionStages.Queries.GetMissionStageById;

public sealed class GetMissionStageByIdQueryHandler(IUnitOfWork unitOfWork, IRepository<MissionStage> missionStageRepository)
    : IRequestHandler<GetMissionStageByIdQuery, MissionStageResponse>
{
    public async Task<MissionStageResponse> Handle(GetMissionStageByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missionStage = await missionStageRepository
            .Include(missionStage => missionStage.Hints)
            .SingleOrDefaultAsync(
                existingMissionStage => existingMissionStage.Id == request.MissionStageId,
                cancellationToken);
        if (missionStage is null)
        {
            throw new UmbralDomainException(
                "mission_stage_not_found",
                $"Mission Stage '{request.MissionStageId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        return missionStage.ToResponse();
    }
}




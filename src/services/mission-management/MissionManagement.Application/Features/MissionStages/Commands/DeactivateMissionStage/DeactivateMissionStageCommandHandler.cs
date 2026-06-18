using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.MissionStages.Commands.DeactivateMissionStage;

public sealed class DeactivateMissionStageCommandHandler(IMissionManagementDbContext dbContext)
    : IRequestHandler<DeactivateMissionStageCommand, MissionStageResponse>
{
    public async Task<MissionStageResponse> Handle(DeactivateMissionStageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missionStage = await dbContext.MissionStages
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

        missionStage.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return missionStage.ToResponse();
    }
}

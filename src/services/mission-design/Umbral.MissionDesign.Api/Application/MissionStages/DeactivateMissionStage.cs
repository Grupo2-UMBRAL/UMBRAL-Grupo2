using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.MissionStages;

public sealed record DeactivateMissionStageCommand(Guid MissionStageId) : IRequest<MissionStageResponse>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
}

public sealed class DeactivateMissionStageCommandHandler(MissionDesignDbContext dbContext)
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

using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.MissionStages;

public sealed record GetMissionStageByIdQuery(Guid MissionStageId) : IRequest<MissionStageResponse>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
}

public sealed class GetMissionStageByIdQueryHandler(IMissionDesignDbContext dbContext)
    : IRequestHandler<GetMissionStageByIdQuery, MissionStageResponse>
{
    public async Task<MissionStageResponse> Handle(GetMissionStageByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missionStage = await dbContext.MissionStages
            .AsNoTracking()
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

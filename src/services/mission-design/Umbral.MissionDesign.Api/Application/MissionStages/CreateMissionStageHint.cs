using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Domain.Missions;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.MissionStages;

public sealed record CreateMissionStageHintCommand(
    Guid MissionStageId,
    string Content,
    bool IsSolution,
    double? Latitude,
    double? Longitude) : IRequest<MissionStageHintResponse>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
}

public sealed class CreateMissionStageHintCommandHandler(MissionDesignDbContext dbContext)
    : IRequestHandler<CreateMissionStageHintCommand, MissionStageHintResponse>
{
    public async Task<MissionStageHintResponse> Handle(
        CreateMissionStageHintCommand request,
        CancellationToken cancellationToken)
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

        var hint = missionStage.AddHint(
            Guid.NewGuid(),
            request.Content,
            request.IsSolution,
            request.Latitude,
            request.Longitude);

        await dbContext.SaveChangesAsync(cancellationToken);

        return hint.ToResponse();
    }
}

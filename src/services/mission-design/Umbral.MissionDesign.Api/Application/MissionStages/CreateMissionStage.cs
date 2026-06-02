using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Domain.Missions;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.MissionStages;

public sealed record CreateMissionStageCommand(
    Guid MissionId,
    string Name,
    int Order,
    string Difficulty,
    string GameType,
    string? ExpectedQrHash,
    string? TriviaValidationCriteria) : IRequest<MissionStageResponse>;

public sealed class CreateMissionStageCommandHandler(MissionDesignDbContext dbContext)
    : IRequestHandler<CreateMissionStageCommand, MissionStageResponse>
{
    public async Task<MissionStageResponse> Handle(CreateMissionStageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missionExists = await dbContext.Missions.AnyAsync(
            mission => mission.Id == request.MissionId,
            cancellationToken);
        if (!missionExists)
        {
            throw new UmbralDomainException(
                "mission_not_found",
                $"Mission '{request.MissionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        var duplicateOrderExists = await dbContext.MissionStages.AnyAsync(
            missionStage => missionStage.MissionId == request.MissionId && missionStage.Order == request.Order,
            cancellationToken);
        if (duplicateOrderExists)
        {
            throw new UmbralDomainException(
                "mission_stage_order_duplicate",
                $"Mission Stage order '{request.Order}' already exists for Mission '{request.MissionId}'.",
                UmbralFailureCategory.Conflict);
        }

        var missionStage = MissionStage.Create(
            Guid.NewGuid(),
            request.MissionId,
            request.Name,
            request.Order,
            request.Difficulty,
            request.GameType,
            request.ExpectedQrHash,
            request.TriviaValidationCriteria);

        dbContext.MissionStages.Add(missionStage);
        await dbContext.SaveChangesAsync(cancellationToken);

        return missionStage.ToResponse();
    }
}

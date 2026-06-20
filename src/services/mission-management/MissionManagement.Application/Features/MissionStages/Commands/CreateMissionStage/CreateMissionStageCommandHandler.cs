using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.MissionStages.Commands.CreateMissionStage;

public sealed class CreateMissionStageCommandHandler(IUnitOfWork unitOfWork, IRepository<Mission> missionRepository, IRepository<MissionStage> missionStageRepository)
    : IRequestHandler<CreateMissionStageCommand, MissionStageResponse>
{
    public async Task<MissionStageResponse> Handle(CreateMissionStageCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missionExists = await missionRepository.AnyAsync(
            mission => mission.Id == request.MissionId,
            cancellationToken);
        if (!missionExists)
        {
            throw new UmbralDomainException(
                "mission_not_found",
                $"Mission '{request.MissionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        var duplicateOrderExists = await missionStageRepository.AnyAsync(
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

        missionStageRepository.Add(missionStage);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return missionStage.ToResponse();
    }
}



using MissionManagement.Domain.Missions;
using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.MissionStages.Commands.CreateMissionStageHint;

public sealed class CreateMissionStageHintCommandHandler(IUnitOfWork unitOfWork, IRepository<MissionStage> missionStageRepository)
    : IRequestHandler<CreateMissionStageHintCommand, MissionStageHintResponse>
{
    public async Task<MissionStageHintResponse> Handle(
        CreateMissionStageHintCommand request,
        CancellationToken cancellationToken)
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

        var hint = missionStage.AddHint(
            Guid.NewGuid(),
            request.Content,
            request.IsSolution,
            request.Latitude,
            request.Longitude);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return hint.ToResponse();
    }
}




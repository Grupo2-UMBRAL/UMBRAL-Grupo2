using MissionManagement.Domain.Entities;
using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Commands.UpdateMission;

public sealed class UpdateMissionCommandHandler(IUnitOfWork unitOfWork, IRepository<Mission> missionRepository)
    : IRequestHandler<UpdateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mission = await missionRepository.SingleOrDefaultAsync(
            existingMission => existingMission.Id == request.MissionId,
            cancellationToken);
        if (mission is null)
        {
            throw new UmbralDomainException(
                "mission_not_found",
                $"Mission '{request.MissionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        mission.UpdateDetails(
            request.Name,
            request.Description,
            request.Difficulty,
            request.MaximumDurationMinutes);
        mission.UpdateCatalogGameType(request.GameType);

        if (request.Nodes is not null)
        {
            mission.ReplaceNodes(request.Nodes.Select(node => node.ToDomain()).ToArray());
        }

        if (mission.IsActive)
        {
            mission.EnsureEligibleForLiveSession();
        }

        var nameAlreadyExists = await missionRepository
            .AnyAsync(
                existingMission => existingMission.Id != request.MissionId && existingMission.Name == mission.Name,
                cancellationToken);
        if (nameAlreadyExists)
        {
            throw new UmbralDomainException(
                "mission_name_duplicate",
                $"Mission '{mission.Name}' already exists.",
                UmbralFailureCategory.Conflict);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}




using MissionManagement.Domain.Missions;
using MediatR;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Commands.UpdateMission;

public sealed class UpdateMissionCommandHandler(
    IUnitOfWork unitOfWork,
    IRepository<Mission> missionRepository,
    IMissionManagementDbContext dbContext)
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
            request.MaximumDurationMinutes);

        await MissionNameUniquenessValidator.EnsureAvailableAsync(
            missionRepository, mission.Name, excludeMissionId: request.MissionId, cancellationToken);

        if (request.Items is null)
        {
            // Scalar-only update: leave the path-item tree untouched, persist and reload for the response.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            var reloaded = await MissionLoader.RequireAsync(dbContext, request.MissionId, cancellationToken);
            return reloaded.ToResponse();
        }

        var rootItems = request.Items.ToDomain(request.MissionId);
        var updatedView = Mission.RehydrateTree(
            mission.Id,
            mission.Name,
            mission.Description,
            mission.MaximumDurationMinutes,
            mission.IsActive,
            rootItems);

        if (mission.IsActive)
        {
            updatedView.EnsureEligibleForLiveSession();
        }

        await MissionLoader.DeleteItemsAsync(dbContext, request.MissionId, cancellationToken);
        MissionLoader.AddItems(dbContext, updatedView);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return updatedView.ToResponse();
    }
}

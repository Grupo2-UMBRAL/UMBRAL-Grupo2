using MediatR;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Commands.UpdateMission;

public sealed class UpdateMissionCommandHandler(IMissionStore missionStore)
    : IRequestHandler<UpdateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mission = await missionStore.GetWithItemsAsync(request.MissionId, cancellationToken);
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
            missionStore, mission.Name, excludeMissionId: request.MissionId, cancellationToken);

        if (request.Items is null)
        {
            // Scalar-only update: the tree stays as loaded, so the response reflects it without a reload.
            await missionStore.UpdateAsync(mission, cancellationToken);
            return mission.ToResponse();
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

        await missionStore.ReplaceItemsAsync(updatedView, cancellationToken);

        return updatedView.ToResponse();
    }
}

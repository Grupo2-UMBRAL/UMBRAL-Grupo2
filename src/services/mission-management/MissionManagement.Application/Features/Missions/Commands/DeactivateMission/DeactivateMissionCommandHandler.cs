using MediatR;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Commands.DeactivateMission;

public sealed class DeactivateMissionCommandHandler(IMissionStore missionStore)
    : IRequestHandler<DeactivateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(DeactivateMissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ponytail: loads the tree even though deactivation only flips a flag — the response body carries
        // the full mission and the web admin re-renders the builder from it (a scalar-only load would blank it).
        var mission = await missionStore.GetWithItemsAsync(request.MissionId, cancellationToken);
        if (mission is null)
        {
            throw new UmbralDomainException(
                "mission_not_found",
                $"Mission '{request.MissionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        mission.Deactivate();
        await missionStore.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}

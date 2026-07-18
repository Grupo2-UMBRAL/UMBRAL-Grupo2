using MediatR;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Commands.ActivateMission;

public sealed class ActivateMissionCommandHandler(IMissionRepository missionRepository)
    : IRequestHandler<ActivateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(ActivateMissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Activation evaluates eligibility over the flattened play path, so the tree must be hydrated.
        var mission = await missionRepository.GetWithItemsAsync(request.MissionId, cancellationToken);
        if (mission is null)
        {
            throw new UmbralDomainException(
                "mission_not_found",
                $"Mission '{request.MissionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        mission.Activate();
        await missionRepository.UpdateAsync(mission, cancellationToken);

        return mission.ToResponse();
    }
}

using MediatR;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Application.Features.Missions.Commands.CreateMission;

public sealed class CreateMissionCommandHandler(IMissionRepository missionRepository)
    : IRequestHandler<CreateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missionId = Guid.NewGuid();
        var rootItems = request.Items?.ToDomain(missionId);

        var mission = Mission.Create(
            missionId,
            request.Name,
            request.Description,
            request.MaximumDurationMinutes,
            rootItems);

        await MissionNameUniquenessValidator.EnsureAvailableAsync(
            missionRepository, mission.Name, excludeMissionId: null, cancellationToken);

        await missionRepository.AddAsync(mission, cancellationToken);

        return mission.ToResponse();
    }
}

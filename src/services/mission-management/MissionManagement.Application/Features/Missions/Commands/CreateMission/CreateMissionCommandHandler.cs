using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Commands.CreateMission;

public sealed class CreateMissionCommandHandler(IUnitOfWork unitOfWork, IRepository<Mission> missionRepository)
    : IRequestHandler<CreateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(CreateMissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mission = Mission.Create(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            request.Difficulty,
            request.MaximumDurationMinutes,
            request.GameType,
            request.Nodes?.Select(node => node.ToDomain()).ToArray());

        var missionNameAlreadyExists = await missionRepository
            .AnyAsync(
                existingMission => existingMission.Name == mission.Name,
                cancellationToken);
        if (missionNameAlreadyExists)
        {
            throw new UmbralDomainException(
                "mission_name_duplicate",
                $"Mission '{mission.Name}' already exists.",
                UmbralFailureCategory.Conflict);
        }

        missionRepository.Add(mission);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}



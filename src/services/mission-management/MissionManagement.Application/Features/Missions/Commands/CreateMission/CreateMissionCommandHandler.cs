using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MissionManagement.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Commands.CreateMission;

public sealed class CreateMissionCommandHandler(
    IUnitOfWork unitOfWork,
    IRepository<Mission> missionRepository,
    IMissionManagementDbContext dbContext)
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
        MissionLoader.AddItems(dbContext, mission);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}

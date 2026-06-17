using MediatR;
using Microsoft.EntityFrameworkCore;
using MissionDesign.Domain.Missions;
using Umbral.ServiceDefaults;

namespace MissionDesign.Application.Features.Missions.Commands.CreateMission;

public sealed class CreateMissionCommandHandler(IMissionDesignDbContext dbContext)
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

        var missionNameAlreadyExists = await dbContext.Missions
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

        dbContext.Missions.Add(mission);
        await dbContext.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}

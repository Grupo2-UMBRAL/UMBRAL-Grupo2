using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Domain.Missions;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record CreateMissionCommand(
    string Name,
    string Description,
    string Difficulty,
    int MaximumDurationMinutes,
    string GameType,
    IReadOnlyList<MissionNodeRequest>? Nodes = null) : IRequest<MissionResponse>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
}

public sealed class CreateMissionCommandHandler(MissionDesignDbContext dbContext)
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

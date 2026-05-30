using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record UpdateMissionCommand(
    Guid MissionId,
    string Name,
    string Description,
    string Difficulty,
    int MaximumDurationMinutes,
    string GameType,
    IReadOnlyList<MissionNodeRequest>? Nodes = null) : IRequest<MissionResponse>;

public sealed class UpdateMissionCommandHandler(MissionDesignDbContext dbContext)
    : IRequestHandler<UpdateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(UpdateMissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mission = await dbContext.Missions.SingleOrDefaultAsync(
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

        var nameAlreadyExists = await dbContext.Missions
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}

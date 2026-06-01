using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record ActivateMissionCommand(Guid MissionId) : IRequest<MissionResponse>;

public sealed class ActivateMissionCommandHandler(MissionDesignDbContext dbContext)
    : IRequestHandler<ActivateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(ActivateMissionCommand request, CancellationToken cancellationToken)
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

        mission.Activate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}

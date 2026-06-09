using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record DeactivateMissionCommand(Guid MissionId) : IRequest<MissionResponse>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
}

public sealed class DeactivateMissionCommandHandler(IMissionDesignDbContext dbContext)
    : IRequestHandler<DeactivateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(DeactivateMissionCommand request, CancellationToken cancellationToken)
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

        mission.Deactivate();
        await dbContext.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}

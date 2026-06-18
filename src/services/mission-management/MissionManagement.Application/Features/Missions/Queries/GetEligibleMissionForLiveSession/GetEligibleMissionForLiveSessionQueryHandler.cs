using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Queries.GetEligibleMissionForLiveSession;

public sealed class GetEligibleMissionForLiveSessionQueryHandler(IMissionManagementDbContext dbContext)
    : IRequestHandler<GetEligibleMissionForLiveSessionQuery, EligibleMissionForLiveSessionResponse>
{
    public async Task<EligibleMissionForLiveSessionResponse> Handle(
        GetEligibleMissionForLiveSessionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mission = await dbContext.Missions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existingMission => existingMission.Id == request.MissionId,
                cancellationToken);
        if (mission is null)
        {
            throw new UmbralDomainException(
                "mission_not_found",
                $"Mission '{request.MissionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        if (!mission.IsActive)
        {
            throw new UmbralDomainException(
                "mission_not_active",
                $"Mission '{mission.Name}' is not active.",
                UmbralFailureCategory.Conflict);
        }

        return mission.ToEligibleForLiveSessionResponse();
    }
}

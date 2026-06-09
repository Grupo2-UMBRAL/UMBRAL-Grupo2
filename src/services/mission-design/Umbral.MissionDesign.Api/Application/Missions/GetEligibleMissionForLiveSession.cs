using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record GetEligibleMissionForLiveSessionQuery(Guid MissionId) : IRequest<EligibleMissionForLiveSessionResponse>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOrOperator;
}

public sealed class GetEligibleMissionForLiveSessionQueryHandler(IMissionDesignDbContext dbContext)
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

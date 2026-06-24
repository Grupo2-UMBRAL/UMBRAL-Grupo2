using MissionManagement.Application.Abstractions;
using MediatR;
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

        var mission = await MissionLoader.RequireAsync(dbContext, request.MissionId, cancellationToken);

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

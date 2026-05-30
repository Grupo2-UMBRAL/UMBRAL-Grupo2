using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Infrastructure;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Application.Missions;

public sealed record GetMissionByIdQuery(Guid MissionId) : IRequest<MissionResponse>;

public sealed class GetMissionByIdQueryHandler(MissionDesignDbContext dbContext)
    : IRequestHandler<GetMissionByIdQuery, MissionResponse>
{
    public async Task<MissionResponse> Handle(GetMissionByIdQuery request, CancellationToken cancellationToken)
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

        return mission.ToResponse();
    }
}

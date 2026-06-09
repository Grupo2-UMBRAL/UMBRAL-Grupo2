using Umbral.ServiceDefaults;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Umbral.MissionDesign.Api.Application.MissionStages;

public sealed record ListMissionStagesQuery(Guid MissionId) : IRequest<IReadOnlyList<MissionStageSummaryResponse>>, IAuthorizableRequest
{
    public RequestAuthorizationMetadata Authorization => UmbralRequestAuthorizations.AdministratorOnly;
}

public sealed class ListMissionStagesQueryHandler(IMissionDesignDbContext dbContext)
    : IRequestHandler<ListMissionStagesQuery, IReadOnlyList<MissionStageSummaryResponse>>
{
    public async Task<IReadOnlyList<MissionStageSummaryResponse>> Handle(
        ListMissionStagesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var missionStages = await dbContext.MissionStages
            .AsNoTracking()
            .Where(missionStage => missionStage.MissionId == request.MissionId)
            .OrderBy(missionStage => missionStage.Order)
            .ThenBy(missionStage => missionStage.Name)
            .Include(missionStage => missionStage.Hints)
            .ToListAsync(cancellationToken);

        return missionStages
            .Select(missionStage => missionStage.ToSummaryResponse())
            .ToList();
    }
}

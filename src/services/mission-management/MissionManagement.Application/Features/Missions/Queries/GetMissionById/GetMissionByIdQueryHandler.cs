using MissionManagement.Application.Abstractions;
using MediatR;

namespace MissionManagement.Application.Features.Missions.Queries.GetMissionById;

public sealed class GetMissionByIdQueryHandler(IMissionManagementDbContext dbContext)
    : IRequestHandler<GetMissionByIdQuery, MissionResponse>
{
    public async Task<MissionResponse> Handle(GetMissionByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mission = await MissionLoader.RequireAsync(dbContext, request.MissionId, cancellationToken);

        return mission.ToResponse();
    }
}

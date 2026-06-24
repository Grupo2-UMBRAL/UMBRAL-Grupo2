using MissionManagement.Domain.Missions;
using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Commands.DeactivateMission;

public sealed class DeactivateMissionCommandHandler(
    IUnitOfWork unitOfWork,
    IRepository<Mission> missionRepository,
    IMissionManagementDbContext dbContext)
    : IRequestHandler<DeactivateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(DeactivateMissionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mission = await missionRepository.SingleOrDefaultAsync(
            existingMission => existingMission.Id == request.MissionId,
            cancellationToken);
        if (mission is null)
        {
            throw new UmbralDomainException(
                "mission_not_found",
                $"Mission '{request.MissionId}' was not found.",
                UmbralFailureCategory.NotFound);
        }

        var aggregate = await MissionLoader.RequireAsync(dbContext, request.MissionId, cancellationToken);
        mission.ReplaceItems(aggregate.RootItems);

        mission.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}

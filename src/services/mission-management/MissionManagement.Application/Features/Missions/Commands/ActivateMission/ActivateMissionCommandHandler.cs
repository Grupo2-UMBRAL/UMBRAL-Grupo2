using MissionManagement.Domain.Missions;
using MissionManagement.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace MissionManagement.Application.Features.Missions.Commands.ActivateMission;

public sealed class ActivateMissionCommandHandler(
    IUnitOfWork unitOfWork,
    IRepository<Mission> missionRepository,
    IMissionManagementDbContext dbContext)
    : IRequestHandler<ActivateMissionCommand, MissionResponse>
{
    public async Task<MissionResponse> Handle(ActivateMissionCommand request, CancellationToken cancellationToken)
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

        // The tracked Mission row has no path-item tree (no EF navigation); hydrate it from the flat
        // rows so eligibility can be evaluated, then flip the (tracked) IsActive flag.
        var aggregate = await MissionLoader.RequireAsync(dbContext, request.MissionId, cancellationToken);
        mission.ReplaceItems(aggregate.RootItems);

        mission.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return mission.ToResponse();
    }
}

using Microsoft.EntityFrameworkCore;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Application.Abstractions;

public interface IMissionManagementDbContext
{
    DbSet<Mission> Missions { get; }

    DbSet<MissionStage> MissionStages { get; }

    DbSet<MissionStageHint> MissionStageHints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

using Microsoft.EntityFrameworkCore;
using MissionDesign.Domain.Missions;

namespace MissionDesign.Application.Abstractions;

public interface IMissionDesignDbContext
{
    DbSet<Mission> Missions { get; }

    DbSet<MissionStage> MissionStages { get; }

    DbSet<MissionStageHint> MissionStageHints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

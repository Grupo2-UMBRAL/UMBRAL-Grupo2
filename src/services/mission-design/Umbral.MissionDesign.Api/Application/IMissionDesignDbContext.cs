using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Domain.Missions;

namespace Umbral.MissionDesign.Api.Application;

public interface IMissionDesignDbContext
{
    DbSet<Mission> Missions { get; }

    DbSet<MissionStage> MissionStages { get; }

    DbSet<MissionStageHint> MissionStageHints { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

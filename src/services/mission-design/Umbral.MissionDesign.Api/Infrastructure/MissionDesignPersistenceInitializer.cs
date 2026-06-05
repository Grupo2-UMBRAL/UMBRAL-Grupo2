using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace Umbral.MissionDesign.Api.Infrastructure;

public sealed class MissionDesignPersistenceInitializer(MissionDesignDbContext dbContext) : IServicePersistenceInitializer
{
    public Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken) =>
        dbContext.Database.MigrateAsync(cancellationToken);
}

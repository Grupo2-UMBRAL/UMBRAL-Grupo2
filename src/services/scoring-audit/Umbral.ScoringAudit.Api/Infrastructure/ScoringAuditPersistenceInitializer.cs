using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace Umbral.ScoringAudit.Api.Infrastructure;

public sealed class ScoringAuditPersistenceInitializer(ScoringAuditDbContext dbContext) : IServicePersistenceInitializer
{
    public Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken) =>
        dbContext.Database.MigrateAsync(cancellationToken);
}

using Microsoft.EntityFrameworkCore;
using Umbral.ServiceDefaults;

namespace Umbral.SessionOperations.Api.Infrastructure;

public sealed class SessionOperationsPersistenceInitializer(SessionOperationsDbContext dbContext) : IServicePersistenceInitializer
{
    public Task ApplyPendingMigrationsAsync(CancellationToken cancellationToken) =>
        dbContext.Database.MigrateAsync(cancellationToken);
}

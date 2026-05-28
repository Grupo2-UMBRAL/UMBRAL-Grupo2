using Microsoft.EntityFrameworkCore;

namespace Umbral.SessionOperations.Api.Infrastructure;

public sealed class SessionOperationsDbContext(DbContextOptions<SessionOperationsDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SessionOperationsPersistence.SchemaName);
    }
}

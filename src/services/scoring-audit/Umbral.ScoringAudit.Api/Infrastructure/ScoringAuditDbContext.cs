using Microsoft.EntityFrameworkCore;

namespace Umbral.ScoringAudit.Api.Infrastructure;

public sealed class ScoringAuditDbContext(DbContextOptions<ScoringAuditDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(ScoringAuditPersistence.SchemaName);
    }
}

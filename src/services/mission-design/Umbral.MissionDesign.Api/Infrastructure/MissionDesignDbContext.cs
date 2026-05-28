using Microsoft.EntityFrameworkCore;

namespace Umbral.MissionDesign.Api.Infrastructure;

public sealed class MissionDesignDbContext(DbContextOptions<MissionDesignDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(MissionDesignPersistence.SchemaName);
    }
}

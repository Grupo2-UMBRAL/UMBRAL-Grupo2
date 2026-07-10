using Microsoft.EntityFrameworkCore;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence;

public sealed class MissionManagementDbContext(DbContextOptions<MissionManagementDbContext> options)
    : DbContext(options), IMissionManagementDbContext
{
    public DbSet<Mission> Missions => Set<Mission>();

    public DbSet<PathItem> PathItems => Set<PathItem>();

    public DbSet<Section> Sections => Set<Section>();

    public DbSet<Challenge> Challenges => Set<Challenge>();

    public DbSet<Play> Plays => Set<Play>();

    public DbSet<Choice> Choices => Set<Choice>();

    public DbSet<Hint> Hints => Set<Hint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(MissionManagementPersistence.SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MissionManagementDbContext).Assembly);
    }
}

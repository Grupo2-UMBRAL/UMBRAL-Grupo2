using Microsoft.EntityFrameworkCore;
using Umbral.MissionDesign.Api.Domain.Missions;

namespace Umbral.MissionDesign.Api.Infrastructure;

public sealed class MissionDesignDbContext(DbContextOptions<MissionDesignDbContext> options) : DbContext(options)
{
    public DbSet<Mission> Missions => Set<Mission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(MissionDesignPersistence.SchemaName);
        modelBuilder.Entity<Mission>(mission =>
        {
            mission.ToTable("missions");
            mission.HasKey(entity => entity.Id);

            mission.Property(entity => entity.Id)
                .ValueGeneratedNever();
            mission.Property(entity => entity.Name)
                .HasMaxLength(120)
                .IsRequired();
            mission.Property(entity => entity.Description)
                .HasMaxLength(1_024)
                .IsRequired();
            mission.Property(entity => entity.Difficulty)
                .HasMaxLength(60)
                .IsRequired();
            mission.Property(entity => entity.MaximumDurationMinutes)
                .IsRequired();
            mission.Property(entity => entity.GameType)
                .HasMaxLength(40)
                .IsRequired();
            mission.Property(entity => entity.IsActive)
                .IsRequired();

            mission.HasIndex(entity => entity.Name)
                .IsUnique();
        });
    }
}

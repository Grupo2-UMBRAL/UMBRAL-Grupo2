using Microsoft.EntityFrameworkCore;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence;

public sealed class MissionManagementDbContext(DbContextOptions<MissionManagementDbContext> options)
    : DbContext(options), IMissionManagementDbContext
{
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionStage> MissionStages => Set<MissionStage>();
    public DbSet<MissionStageHint> MissionStageHints => Set<MissionStageHint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(MissionManagementPersistence.SchemaName);
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
            mission.Property(entity => entity.NodeTreeJson)
                .HasColumnType("text")
                .IsRequired();

            mission.HasIndex(entity => entity.Name)
                .IsUnique();
        });

        modelBuilder.Entity<MissionStage>(missionStage =>
        {
            missionStage.ToTable("mission_stages");
            missionStage.HasKey(entity => entity.Id);

            missionStage.Property(entity => entity.Id)
                .ValueGeneratedNever();
            missionStage.Property(entity => entity.MissionId)
                .IsRequired();
            missionStage.Property(entity => entity.Name)
                .HasMaxLength(120)
                .IsRequired();
            missionStage.Property(entity => entity.Order)
                .IsRequired();
            missionStage.Property(entity => entity.Difficulty)
                .HasMaxLength(16)
                .IsRequired();
            missionStage.Property(entity => entity.GameType)
                .HasMaxLength(40)
                .IsRequired();
            missionStage.Property(entity => entity.ExpectedQrHash)
                .HasMaxLength(256);
            missionStage.Property(entity => entity.TriviaValidationCriteria)
                .HasMaxLength(1_024);
            missionStage.Property(entity => entity.IsActive)
                .IsRequired();

            missionStage.HasOne<Mission>()
                .WithMany()
                .HasForeignKey(entity => entity.MissionId)
                .OnDelete(DeleteBehavior.Cascade);

            missionStage.HasMany(entity => entity.Hints)
                .WithOne()
                .HasForeignKey(entity => entity.MissionStageId)
                .OnDelete(DeleteBehavior.Cascade);

            missionStage.HasIndex(entity => new { entity.MissionId, entity.Order })
                .IsUnique();
        });

        modelBuilder.Entity<MissionStageHint>(missionStageHint =>
        {
            missionStageHint.ToTable("mission_stage_hints");
            missionStageHint.HasKey(entity => entity.Id);

            missionStageHint.Property(entity => entity.Id)
                .ValueGeneratedNever();
            missionStageHint.Property(entity => entity.MissionStageId)
                .IsRequired();
            missionStageHint.Property(entity => entity.Content)
                .HasMaxLength(1_024)
                .IsRequired();
            missionStageHint.Property(entity => entity.IsSolution)
                .IsRequired();
            missionStageHint.Property(entity => entity.Latitude);
            missionStageHint.Property(entity => entity.Longitude);
        });
    }
}


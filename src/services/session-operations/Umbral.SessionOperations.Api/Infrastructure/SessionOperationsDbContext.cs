using Microsoft.EntityFrameworkCore;
using Umbral.SessionOperations.Api.Domain.LiveSessions;

namespace Umbral.SessionOperations.Api.Infrastructure;

public sealed class SessionOperationsDbContext(DbContextOptions<SessionOperationsDbContext> options) : DbContext(options)
{
    public DbSet<LiveSession> LiveSessions => Set<LiveSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SessionOperationsPersistence.SchemaName);
        modelBuilder.Entity<LiveSession>(liveSession =>
        {
            liveSession.ToTable("live_sessions");
            liveSession.HasKey(entity => entity.Id);

            liveSession.Property(entity => entity.Id)
                .ValueGeneratedNever();
            liveSession.Property(entity => entity.MissionId)
                .IsRequired();
            liveSession.Property(entity => entity.MissionName)
                .HasMaxLength(120)
                .IsRequired();
            liveSession.Property(entity => entity.Name)
                .HasMaxLength(120)
                .IsRequired();
            liveSession.Property(entity => entity.State)
                .HasMaxLength(40)
                .IsRequired();
            liveSession.Property(entity => entity.ScheduledStartAtUtc);
            liveSession.Property(entity => entity.CreatedAtUtc)
                .IsRequired();
            liveSession.Property(entity => entity.SessionStageFlowJson)
                .HasColumnType("text")
                .IsRequired();

            liveSession.HasIndex(entity => entity.MissionId);
            liveSession.HasIndex(entity => entity.CreatedAtUtc);
        });
    }
}

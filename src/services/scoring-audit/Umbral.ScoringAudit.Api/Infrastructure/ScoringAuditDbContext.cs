using Microsoft.EntityFrameworkCore;
using Umbral.ScoringAudit.Api.Domain.Scoreboards;

namespace Umbral.ScoringAudit.Api.Infrastructure;

public sealed class ScoringAuditDbContext(DbContextOptions<ScoringAuditDbContext> options) : DbContext(options)
{
    public DbSet<Scoreboard> Scoreboards => Set<Scoreboard>();

    public DbSet<ScoreEntry> ScoreEntries => Set<ScoreEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(ScoringAuditPersistence.SchemaName);

        modelBuilder.Entity<Scoreboard>(scoreboard =>
        {
            scoreboard.ToTable("scoreboards");
            scoreboard.HasKey(entity => entity.LiveSessionId);

            scoreboard.Property(entity => entity.LiveSessionId)
                .HasColumnName("live_session_id")
                .ValueGeneratedNever();

            scoreboard.Ignore(entity => entity.TeamScores);
            scoreboard.HasMany(entity => entity.ScoreEntries)
                .WithOne()
                .HasForeignKey(entity => entity.LiveSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            scoreboard.Navigation(entity => entity.ScoreEntries)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ScoreEntry>(scoreEntry =>
        {
            scoreEntry.ToTable("score_entries");
            scoreEntry.HasKey(entity => entity.ScoreEntryId);

            scoreEntry.Property(entity => entity.ScoreEntryId)
                .HasColumnName("score_entry_id")
                .ValueGeneratedNever();
            scoreEntry.Property(entity => entity.LiveSessionId)
                .HasColumnName("live_session_id")
                .IsRequired();
            scoreEntry.Property(entity => entity.SessionTeamId)
                .HasColumnName("session_team_id")
                .IsRequired();
            scoreEntry.Property(entity => entity.EntryType)
                .HasColumnName("entry_type")
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();
            scoreEntry.Property(entity => entity.Delta)
                .HasColumnName("delta")
                .IsRequired();
            scoreEntry.Property(entity => entity.AccumulatedScoreBefore)
                .HasColumnName("accumulated_score_before")
                .IsRequired();
            scoreEntry.Property(entity => entity.AccumulatedScoreAfter)
                .HasColumnName("accumulated_score_after")
                .IsRequired();
            scoreEntry.Property(entity => entity.VisibleScoreBefore)
                .HasColumnName("visible_score_before")
                .IsRequired();
            scoreEntry.Property(entity => entity.VisibleScoreAfter)
                .HasColumnName("visible_score_after")
                .IsRequired();
            scoreEntry.Property(entity => entity.RecordedAt)
                .HasColumnName("recorded_at")
                .IsRequired();
            scoreEntry.Property(entity => entity.MissionStageId)
                .HasColumnName("mission_stage_id");
            scoreEntry.Property(entity => entity.PenaltyCommandId)
                .HasColumnName("penalty_command_id");
            scoreEntry.Property(entity => entity.PenaltyId)
                .HasColumnName("penalty_id");
            scoreEntry.Property(entity => entity.PenaltySeverity)
                .HasColumnName("penalty_severity")
                .HasConversion<string>()
                .HasMaxLength(40);
            scoreEntry.Property(entity => entity.ResolutionTime)
                .HasColumnName("resolution_time")
                .HasColumnType("interval");

            scoreEntry.HasIndex(entity => entity.LiveSessionId)
                .HasDatabaseName("ix_score_entries_live_session_id");
            scoreEntry.HasIndex(entity => new { entity.LiveSessionId, entity.SessionTeamId })
                .HasDatabaseName("ix_score_entries_live_session_id_session_team_id");
            scoreEntry.HasIndex(entity => new { entity.LiveSessionId, entity.SessionTeamId, entity.MissionStageId })
                .IsUnique()
                .HasDatabaseName("ux_score_entries_single_stage_credit")
                .HasFilter("mission_stage_id IS NOT NULL AND entry_type IN ('StageCredit', 'ValidationOverrideCredit')");
        });
    }
}

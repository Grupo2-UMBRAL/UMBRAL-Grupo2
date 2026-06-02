using Microsoft.EntityFrameworkCore;
using Umbral.SessionOperations.Api.Domain.LiveSessions;

namespace Umbral.SessionOperations.Api.Infrastructure;

public sealed class SessionOperationsDbContext(DbContextOptions<SessionOperationsDbContext> options) : DbContext(options)
{
    public DbSet<LiveSession> LiveSessions => Set<LiveSession>();

    public DbSet<SessionTeam> SessionTeams => Set<SessionTeam>();

    public DbSet<TeamParticipation> TeamParticipations => Set<TeamParticipation>();

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
            liveSession.Property(entity => entity.JoinCodeValue)
                .HasColumnName("join_code_value")
                .HasMaxLength(JoinCode.Length);
            liveSession.Property(entity => entity.EnrollmentWindowOpenedAtUtc)
                .HasColumnName("enrollment_window_opened_at_utc");
            liveSession.Property(entity => entity.EnrollmentWindowClosedAtUtc)
                .HasColumnName("enrollment_window_closed_at_utc");

            liveSession.HasIndex(entity => entity.MissionId);
            liveSession.HasIndex(entity => entity.CreatedAtUtc);
            liveSession.HasIndex(entity => entity.JoinCodeValue)
                .IsUnique()
                .HasDatabaseName("ix_live_sessions_join_code_value")
                .HasFilter("join_code_value IS NOT NULL");
            liveSession.HasMany(entity => entity.SessionTeams)
                .WithOne()
                .HasForeignKey(entity => entity.LiveSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            liveSession.HasMany(entity => entity.TeamParticipations)
                .WithOne()
                .HasForeignKey(entity => entity.LiveSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionTeam>(sessionTeam =>
        {
            sessionTeam.ToTable("session_teams");
            sessionTeam.HasKey(entity => entity.Id);

            sessionTeam.Property(entity => entity.Id)
                .ValueGeneratedNever();
            sessionTeam.Property(entity => entity.LiveSessionId)
                .IsRequired();
            sessionTeam.Property(entity => entity.Name)
                .HasMaxLength(SessionTeam.NameMaximumLength)
                .IsRequired();
            sessionTeam.Property(entity => entity.NormalizedName)
                .HasMaxLength(SessionTeam.NameMaximumLength)
                .IsRequired();
            sessionTeam.Property(entity => entity.CreatedAtUtc)
                .IsRequired();

            sessionTeam.HasIndex(entity => new { entity.LiveSessionId, entity.NormalizedName })
                .IsUnique()
                .HasDatabaseName("ix_session_teams_live_session_id_normalized_name");
        });

        modelBuilder.Entity<TeamParticipation>(teamParticipation =>
        {
            teamParticipation.ToTable("team_participations");
            teamParticipation.HasKey(entity => entity.Id);

            teamParticipation.Property(entity => entity.Id)
                .ValueGeneratedNever();
            teamParticipation.Property(entity => entity.LiveSessionId)
                .IsRequired();
            teamParticipation.Property(entity => entity.SessionTeamId)
                .IsRequired();
            teamParticipation.Property(entity => entity.ParticipantUserId)
                .HasMaxLength(ParticipantUserId.MaximumLength)
                .IsRequired();
            teamParticipation.Property(entity => entity.EnrolledAtUtc)
                .IsRequired();

            teamParticipation.HasIndex(entity => new { entity.LiveSessionId, entity.ParticipantUserId })
                .IsUnique()
                .HasDatabaseName("ix_team_participations_live_session_id_participant_user_id");
            teamParticipation.HasOne<SessionTeam>()
                .WithMany()
                .HasForeignKey(entity => entity.SessionTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

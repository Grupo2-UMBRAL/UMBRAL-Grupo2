using Microsoft.EntityFrameworkCore;
using SessionManagement.Application.Abstractions;
using SessionManagement.Domain.LiveSessions;

namespace SessionManagement.Infrastructure.Persistence;

public sealed class SessionManagementDbContext(DbContextOptions<SessionManagementDbContext> options) : DbContext(options), ISessionManagementDbContext
{
    public DbSet<LiveSession> LiveSessions => Set<LiveSession>();

    public DbSet<SessionTeam> SessionTeams => Set<SessionTeam>();

    public DbSet<TeamParticipation> TeamParticipations => Set<TeamParticipation>();

    public DbSet<SessionTeamProgress> TeamProgressions => Set<SessionTeamProgress>();

    public DbSet<EvidenceSubmission> EvidenceSubmissions => Set<EvidenceSubmission>();

    public DbSet<ValidationOverrideLog> ValidationOverrideLogs => Set<ValidationOverrideLog>();

    public DbSet<ReleasedHint> ReleasedHints => Set<ReleasedHint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.HasDefaultSchema(SessionManagementPersistence.SchemaName);
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
            liveSession.Property(entity => entity.SequenceNumber)
                .HasColumnName("sequence_number")
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
            liveSession.HasMany(entity => entity.TeamProgressions)
                .WithOne()
                .HasForeignKey(entity => entity.LiveSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            liveSession.HasMany(entity => entity.EvidenceSubmissions)
                .WithOne()
                .HasForeignKey(entity => entity.LiveSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            liveSession.HasMany(entity => entity.ValidationOverrideLogs)
                .WithOne()
                .HasForeignKey(entity => entity.LiveSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            liveSession.HasMany(entity => entity.ReleasedHints)
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

        modelBuilder.Entity<SessionTeamProgress>(teamProgress =>
        {
            teamProgress.ToTable("session_team_progressions");
            teamProgress.HasKey(entity => entity.Id);

            teamProgress.Property(entity => entity.Id)
                .ValueGeneratedNever();
            teamProgress.Property(entity => entity.LiveSessionId)
                .IsRequired();
            teamProgress.Property(entity => entity.SessionTeamId)
                .IsRequired();
            teamProgress.Property(entity => entity.CurrentStageIndex)
                .IsRequired();
            teamProgress.Property(entity => entity.State)
                .HasMaxLength(40)
                .IsRequired();
            teamProgress.Property(entity => entity.UpdatedAtUtc)
                .IsRequired();

            teamProgress.HasIndex(entity => new { entity.LiveSessionId, entity.SessionTeamId })
                .IsUnique()
                .HasDatabaseName("ix_session_team_progressions_live_session_id_session_team_id");
            teamProgress.HasOne<SessionTeam>()
                .WithMany()
                .HasForeignKey(entity => entity.SessionTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EvidenceSubmission>(evidenceSubmission =>
        {
            evidenceSubmission.ToTable("evidence_submissions");
            evidenceSubmission.HasKey(entity => entity.Id);

            evidenceSubmission.Property(entity => entity.Id)
                .ValueGeneratedNever();
            evidenceSubmission.Property(entity => entity.LiveSessionId)
                .IsRequired();
            evidenceSubmission.Property(entity => entity.SessionTeamId)
                .IsRequired();
            evidenceSubmission.Property(entity => entity.MissionStageId)
                .IsRequired();
            evidenceSubmission.Property(entity => entity.GameType)
                .HasMaxLength(40)
                .IsRequired();
            evidenceSubmission.Property(entity => entity.SubmittedHash)
                .HasMaxLength(EvidenceSubmission.SubmittedHashMaximumLength)
                .IsRequired(false);
            evidenceSubmission.Property(entity => entity.SubmittedText)
                .HasMaxLength(EvidenceSubmission.SubmittedTextMaximumLength)
                .IsRequired(false);
            evidenceSubmission.Property(entity => entity.Outcome)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            evidenceSubmission.Property(entity => entity.FailureReason)
                .HasMaxLength(EvidenceSubmission.FailureReasonMaximumLength);
            evidenceSubmission.Property(entity => entity.SubmittedAtUtc)
                .IsRequired();

            evidenceSubmission.HasIndex(entity => new
                {
                    entity.LiveSessionId,
                    entity.SessionTeamId,
                    entity.MissionStageId
                })
                .HasDatabaseName("ix_evidence_submissions_live_session_team_stage");
            evidenceSubmission.HasOne<SessionTeam>()
                .WithMany()
                .HasForeignKey(entity => entity.SessionTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ValidationOverrideLog>(validationOverrideLog =>
        {
            validationOverrideLog.ToTable("validation_override_logs");
            validationOverrideLog.HasKey(entity => entity.Id);

            validationOverrideLog.Property(entity => entity.Id)
                .ValueGeneratedNever();
            validationOverrideLog.Property(entity => entity.LiveSessionId)
                .IsRequired();
            validationOverrideLog.Property(entity => entity.EvidenceSubmissionId)
                .IsRequired();
            validationOverrideLog.Property(entity => entity.SessionTeamId)
                .IsRequired();
            validationOverrideLog.Property(entity => entity.MissionStageId)
                .IsRequired();
            validationOverrideLog.Property(entity => entity.OperatorUserId)
                .HasMaxLength(ValidationOverrideLog.OperatorUserIdMaximumLength)
                .IsRequired();
            validationOverrideLog.Property(entity => entity.Reason)
                .HasMaxLength(ValidationOverrideLog.ReasonMaximumLength)
                .IsRequired();
            validationOverrideLog.Property(entity => entity.PreviousOutcome)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            validationOverrideLog.Property(entity => entity.NewOutcome)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
            validationOverrideLog.Property(entity => entity.OverriddenAtUtc)
                .IsRequired();

            validationOverrideLog.HasIndex(entity => entity.LiveSessionId)
                .HasDatabaseName("IX_validation_override_logs_LiveSessionId");
            validationOverrideLog.HasIndex(entity => entity.EvidenceSubmissionId)
                .HasDatabaseName("ix_validation_override_logs_evidence_submission_id");
            validationOverrideLog.HasIndex(entity => new
                {
                    entity.LiveSessionId,
                    entity.SessionTeamId,
                    entity.MissionStageId
                })
                .HasDatabaseName("ix_validation_override_logs_live_session_team_stage");
            validationOverrideLog.HasOne<EvidenceSubmission>()
                .WithMany()
                .HasForeignKey(entity => entity.EvidenceSubmissionId)
                .OnDelete(DeleteBehavior.Restrict);
            validationOverrideLog.HasOne<SessionTeam>()
                .WithMany()
                .HasForeignKey(entity => entity.SessionTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReleasedHint>(releasedHint =>
        {
            releasedHint.ToTable("released_hints");
            releasedHint.HasKey(entity => entity.Id);

            releasedHint.Property(entity => entity.Id)
                .ValueGeneratedNever();
            releasedHint.Property(entity => entity.LiveSessionId)
                .IsRequired();
            releasedHint.Property(entity => entity.SessionTeamId)
                .IsRequired();
            releasedHint.Property(entity => entity.MissionStageId)
                .IsRequired();
            releasedHint.Property(entity => entity.HintId)
                .IsRequired();
            releasedHint.Property(entity => entity.ReleasedAtUtc)
                .IsRequired();
            releasedHint.Property(entity => entity.UnlockReason)
                .HasMaxLength(ReleasedHint.UnlockReasonMaximumLength)
                .IsRequired();

            releasedHint.HasIndex(entity => new
                {
                    entity.LiveSessionId,
                    entity.SessionTeamId,
                    entity.MissionStageId,
                    entity.HintId
                })
                .IsUnique()
                .HasDatabaseName("ix_released_hints_session_team_hint");
            releasedHint.HasIndex(entity => new { entity.LiveSessionId, entity.SessionTeamId })
                .HasDatabaseName("ix_released_hints_live_session_team");
            releasedHint.HasOne<SessionTeam>()
                .WithMany()
                .HasForeignKey(entity => entity.SessionTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}

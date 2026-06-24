using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence.Configurations;

internal sealed class ChallengeConfiguration : IEntityTypeConfiguration<Challenge>
{
    public void Configure(EntityTypeBuilder<Challenge> builder)
    {
        // Shares the "title" column with Section (TPH).
        builder.Property(challenge => challenge.Title)
            .HasColumnName("title")
            .HasMaxLength(120);
        builder.Property(challenge => challenge.GameType)
            .HasColumnName("game_type")
            .HasMaxLength(40);
        builder.Property(challenge => challenge.DefaultDifficulty)
            .HasColumnName("default_difficulty")
            .HasMaxLength(16);
        builder.Property(challenge => challenge.DefaultTimeLimitMinutes)
            .HasColumnName("default_time_limit_minutes");
        builder.Property(challenge => challenge.IsActive)
            .HasColumnName("is_active");

        // A Challenge owns its plays (Play TPH base, keyed by ChallengeId). Cascade on delete.
        builder.HasMany(challenge => challenge.Plays)
            .WithOne()
            .HasForeignKey(play => play.ChallengeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(challenge => challenge.Plays)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

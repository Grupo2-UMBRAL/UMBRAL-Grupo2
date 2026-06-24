using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MissionManagement.Domain.Missions;

namespace MissionManagement.Infrastructure.Persistence.Configurations;

/// <summary>TPH base for plays. <c>play_kind</c> discriminates Question vs Search.</summary>
internal sealed class PlayConfiguration : IEntityTypeConfiguration<Play>
{
    public const string QuestionDiscriminator = "Question";
    public const string SearchDiscriminator = "Search";

    public void Configure(EntityTypeBuilder<Play> builder)
    {
        builder.ToTable("plays");
        builder.HasKey(play => play.Id);

        builder.Property(play => play.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(play => play.ChallengeId)
            .HasColumnName("challenge_id")
            .IsRequired();
        builder.Property(play => play.Order)
            .HasColumnName("order")
            .IsRequired();
        builder.Property(play => play.DifficultyOverride)
            .HasColumnName("difficulty_override")
            .HasMaxLength(16);
        builder.Property(play => play.TimeLimitMinutesOverride)
            .HasColumnName("time_limit_minutes_override");

        builder.HasDiscriminator<string>("play_kind")
            .HasValue<Question>(QuestionDiscriminator)
            .HasValue<Search>(SearchDiscriminator);

        builder.Property("play_kind")
            .HasColumnName("play_kind")
            .HasMaxLength(20);

        builder.HasIndex(play => new { play.ChallengeId, play.Order })
            .IsUnique();
    }
}
